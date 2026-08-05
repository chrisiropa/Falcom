using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Falcom
{
   public class Worker : BackgroundService
   {
      private readonly ILogger<Worker> _logger;
      private readonly ConfigManager _configManager;
      private readonly OPC_Client_Crane _opcClientCrane;
      private readonly FalcomEventQueue _eventQueue; // NEU: Die Event-Queue injizieren
      private readonly WatchdogSender _watchdogSender;
      private readonly AktuelleFahrtRepository _aktuelleFahrtRepository;
      private readonly BunkerMaterialRepository _bunkerMaterialRepository;
      private readonly KranPositionenRepository _kranPositionenRepository;
      private readonly FalcomRuntimeStatus _runtimeStatus;
      private ProcessState _currentState;
      private ProcessState? _lastLoggedState;
      private int watchdogEventPending;
      private int watchdogValue;
      private int watchdogEventsInCurrentMinute;
      private DateTime nextWatchdogSummaryUtc = DateTime.UtcNow.AddMinutes(1);

      public Worker(
          ILogger<Worker> logger,
          ConfigManager configManager,
          OPC_Client_Crane opcClientCrane,
          FalcomEventQueue eventQueue,
          WatchdogSender watchdogSender,
          AktuelleFahrtRepository aktuelleFahrtRepository,
          BunkerMaterialRepository bunkerMaterialRepository,
          KranPositionenRepository kranPositionenRepository,
          FalcomRuntimeStatus runtimeStatus) // Im Konstruktor uebergeben
      {
         _logger = logger;
         _configManager = configManager;
         _opcClientCrane = opcClientCrane;
         _eventQueue = eventQueue; // NEU
         _watchdogSender = watchdogSender;
         _aktuelleFahrtRepository = aktuelleFahrtRepository;
         _bunkerMaterialRepository = bunkerMaterialRepository;
         _kranPositionenRepository = kranPositionenRepository;
         _runtimeStatus = runtimeStatus;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         try
         {
            _logger.LogInformation("002E|State machine started.");
            _logger.LogInformation("004C|Initialer Verbindungsaufbau zur Kran-SPS wird gestartet.");
            await _opcClientCrane.ConnectUntilConnectedAsync(stoppingToken);
            _logger.LogInformation("004D|Initialer Verbindungsaufbau zur Kran-SPS ist abgeschlossen.");

            using var watchdogTimerCancellation =
               CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task watchdogTimer = ScheduleWatchdogEventsAsync(
               watchdogTimerCancellation.Token);
            Task opcDataFlowMonitor = ScheduleOpcDataFlowChecksAsync(
               watchdogTimerCancellation.Token);
            Task spsResendMonitor = ScheduleSpsResendChecksAsync(
               watchdogTimerCancellation.Token);

            // NEU: Die Schleife wartet jetzt reaktiv, bis ein Event in der Queue landet.
            // WaitToReadAsync laesst den Thread schlafen, solange die Queue leer ist (0% CPU Last).
            try
            {
               InitializeStateFromDatabase();

               while (await _eventQueue.Reader.WaitToReadAsync(stoppingToken))
               {
                  // Verarbeite alle aktuell in der Queue liegenden Events der Reihe nach (FIFO)
                  while (_eventQueue.Reader.TryRead(out var falcomEvent))
                  {
                     try
                     {
                        _logger.LogDebug("0030|Dispatcher verarbeitet Event {EventId} von Quelle {Source}.", falcomEvent.EventId, falcomEvent.Source);

                        if (falcomEvent is WatchdogEvent watchdogEvent)
                        {
                           try
                           {
                              watchdogEventsInCurrentMinute++;
                              LogWatchdogSummaryIfDue(watchdogEvent.LebensZaehler);

                              await _watchdogSender.SendAsync(
                                 watchdogEvent.LebensZaehler,
                                 stoppingToken);
                           }
                           finally
                           {
                              Interlocked.Exchange(
                                 ref watchdogEventPending,
                                 0);
                           }

                           continue;
                        }

                        if (falcomEvent is KranSpsLebensZaehlerEvent)
                        {
                           continue;
                        }

                        if (falcomEvent is BunkerMaterialAnforderungEvent bunkerAnforderung)
                        {
                           _logger.LogInformation(
                              "01D4|Event_204 wird verarbeitet. AnforderungsZaehler={AnforderungsZaehler}, Initialwert={IstInitialwert}.",
                              bunkerAnforderung.AnforderungsZaehler,
                              bunkerAnforderung.IstInitialwert);

                           BunkerMaterialSnapshot snapshot = _bunkerMaterialRepository.GetSnapshot();
                           OPC_Client_Crane.OpcSendResult antwort =
                              await _opcClientCrane.SendBunkerMaterialResponseAsync(
                                 bunkerAnforderung.AnforderungsZaehler,
                                 snapshot,
                                 stoppingToken);

                           if (!antwort.Success)
                           {
                              _logger.LogError(
                                 "01D5|Event_104 konnte nicht beantwortet werden. AnforderungsZaehler={AnforderungsZaehler}, Grund={Reason}.",
                                 bunkerAnforderung.AnforderungsZaehler,
                                 antwort.Reason);
                           }
                           else
                           {
                              _logger.LogInformation(
                                 "01D6|Event_104 beantwortet. AnforderungsZaehler={AnforderungsZaehler}, AnzahlBunker={AnzahlBunker}.",
                                 bunkerAnforderung.AnforderungsZaehler,
                                 snapshot.AnzahlBunker);
                           }

                           continue;
                        }

                        if (falcomEvent is KranPositionenAnforderungEvent positionenAnforderung)
                        {
                           _logger.LogInformation(
                              "01F4|Event_206 wird verarbeitet. AnforderungsZaehler={AnforderungsZaehler}, Initialwert={IstInitialwert}.",
                              positionenAnforderung.AnforderungsZaehler,
                              positionenAnforderung.IstInitialwert);

                           KranPositionenSnapshot snapshot = _kranPositionenRepository.GetSnapshot();
                           OPC_Client_Crane.OpcSendResult antwort =
                              await _opcClientCrane.SendKranPositionenResponseAsync(
                                 positionenAnforderung.AnforderungsZaehler,
                                 snapshot,
                                 stoppingToken);

                           if (!antwort.Success)
                           {
                              _logger.LogError(
                                 "01F5|Event_106 konnte nicht beantwortet werden. AnforderungsZaehler={AnforderungsZaehler}, Grund={Reason}.",
                                 positionenAnforderung.AnforderungsZaehler,
                                 antwort.Reason);
                           }
                           else
                           {
                              _logger.LogInformation(
                                 "01F6|Event_106 beantwortet. AnforderungsZaehler={AnforderungsZaehler}, AnzahlPositionen={AnzahlPositionen}.",
                                 positionenAnforderung.AnforderungsZaehler,
                                 snapshot.AnzahlPositionen);
                           }

                           continue;
                        }

                        if (falcomEvent is NextKranfahrtAvailableEvent nextKranfahrtAvailableEvent)
                        {
                           SetState(ProcessState.AuftragBereit);

                           _logger.LogInformation(
                              "0008|Feuere Verarbeitung der naechsten Kranfahrt ab: Typ={AuftragsTyp}, AuftragID={AuftragID}, Grund={Reason}.",
                              nextKranfahrtAvailableEvent.AuftragsTyp,
                              nextKranfahrtAvailableEvent.AuftragID,
                              nextKranfahrtAvailableEvent.Reason);

                           AktuelleFahrtResult result =
                              _aktuelleFahrtRepository.TryCreateNextAktuelleFahrt(null);

                           _logger.LogInformation(
                              "0045|Aktuelle Fahrt aus naechster DB-Kranfahrt erzeugt: Erfolg={Success}, Grund={Reason}, AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Teilfahrt={AuftragTeilfahrt}, Typ={AuftragsTyp}, Quelle={Quelle}, QuelleUnterposition={QuelleUnterposition}, Ziel={Ziel}, ZielUnterposition={ZielUnterposition}, SollMengeKg={SollMengeKg}.",
                              result.Success,
                              result.Reason,
                              result.AktuelleFahrtID,
                              result.AuftragID,
                              result.AuftragTeilfahrt,
                              result.AuftragsTyp,
                              result.Quelle,
                              result.QuelleUnterposition,
                              result.Ziel,
                              result.ZielUnterposition,
                              result.SollMengeKg);

                           if (result.Success)
                           {
                              _runtimeStatus.SetAktuelleFahrt(result);

                              KranfahrtAuftragEvent kranfahrtAuftragEvent =
                                 KranfahrtAuftragEvent.FromAktuelleFahrt(
                                    result,
                                    auftragTeilfahrt: result.AuftragTeilfahrt ?? 1,
                                    toleranzKg: 150m);

                              OPC_Client_Crane.OpcSendResult sendResult =
                                 await _opcClientCrane.SendKranfahrtAuftragAsync(
                                    kranfahrtAuftragEvent,
                                    stoppingToken);

                              if (!sendResult.Success)
                              {
                                 AktuelleFahrtResult sendFailureResult =
                                    _aktuelleFahrtRepository.MarkSpsSendFailure(
                                       result.AktuelleFahrtID,
                                       sendResult.Reason);

                                 _runtimeStatus.SetAktuelleFahrt(sendFailureResult);

                                 _logger.LogWarning(
                                    "0110|Technischer SPS-Sendefehler. Aktuelle Fahrt bleibt aktiv und wird automatisch erneut gesendet: AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Teilfahrt={AuftragTeilfahrt}, Grund={Reason}.",
                                    sendFailureResult.AktuelleFahrtID,
                                    sendFailureResult.AuftragID,
                                    sendFailureResult.AuftragTeilfahrt,
                                    sendResult.Reason);

                                 SetState(ProcessState.OpcGestoert);
                              }
                              else
                              {
                                 AktuelleFahrtResult sentResult =
                                    _aktuelleFahrtRepository.MarkSpsSendSuccess(
                                       result.AktuelleFahrtID,
                                       sendResult.TelegrammNummer,
                                       sendResult.ZaehlerAnfahrt);

                                 _runtimeStatus.SetAktuelleFahrt(sentResult);

                                 _logger.LogInformation(
                                    "0086|SPS-Fahrauftrag als gesendet markiert: AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Teilfahrt={AuftragTeilfahrt}, TelegrammNummer={TelegrammNummer}.",
                                    sentResult.AktuelleFahrtID,
                                    sentResult.AuftragID,
                                    sentResult.AuftragTeilfahrt,
                                    sendResult.TelegrammNummer);

                                 SetState(ProcessState.FahrtAnSpsGesendet);
                                 SetState(ProcessState.WarteAufSpsRueckmeldung);
                              }
                           }
                           else
                           {
                              SetState(ProcessState.Fehler);
                           }
                        }

                        if (falcomEvent is KranfahrtBeendetEvent kranfahrtBeendetEvent)
                        {
                           _logger.LogInformation(
                              "0084|KranfahrtBeendet wird an Datenbank uebergeben: Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, Quelle={Quelle}, Ziel={Ziel}, Status={Status}, IstGewicht={IstGewicht}, AenderungsZaehler={AenderungsZaehler}.",
                              kranfahrtBeendetEvent.AuftragsNummer,
                              kranfahrtBeendetEvent.TeilfahrtID,
                              kranfahrtBeendetEvent.KranQuelle,
                              kranfahrtBeendetEvent.KranZiel,
                              kranfahrtBeendetEvent.Status,
                              kranfahrtBeendetEvent.IstGewicht,
                              kranfahrtBeendetEvent.ÄnderungsZähler);

                           AktuelleFahrtResult result =
                              _aktuelleFahrtRepository.CompleteAktuelleFahrt(
                                 kranfahrtBeendetEvent);

                           _logger.LogInformation(
                              "0046|KranfahrtBeendet verarbeitet: Erfolg={Success}, Grund={Reason}, AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Typ={AuftragsTyp}, IstMengeKg={IstMengeKg}.",
                              result.Success,
                              result.Reason,
                              result.AktuelleFahrtID,
                              result.AuftragID,
                              result.AuftragsTyp,
                              result.IstMengeKg);

                           if (result.Success)
                           {
                              SetState(ProcessState.FahrtAbgeschlossen);
                              SetState(ProcessState.Idle);
                           }
                           else
                           {
                              SetState(ProcessState.Fehler);
                           }
                        }

                        if (falcomEvent is LkwPlatzLeer207Event lkwPlatzLeerEvent)
                        {
                           _logger.LogInformation(
                              "01B3|Event_207 wird an die Datenbank uebergeben: Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, LkwPlatz={LkwPlatz}, AenderungsZaehler={AenderungsZaehler}.",
                              lkwPlatzLeerEvent.AuftragsNummer,
                              lkwPlatzLeerEvent.TeilfahrtID,
                              lkwPlatzLeerEvent.LkwPlatzPositionID,
                              lkwPlatzLeerEvent.AenderungsZaehler);

                           AktuelleFahrtResult result =
                              _aktuelleFahrtRepository.CompleteEinlagerAuftragLkwLeer(
                                 lkwPlatzLeerEvent);

                           if (result.Success)
                           {
                              _logger.LogInformation(
                                 "01B4|Einlagerauftrag durch Event_207 regulaer beendet: Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, LkwPlatz={LkwPlatz}, Grund={Reason}.",
                                 lkwPlatzLeerEvent.AuftragsNummer,
                                 lkwPlatzLeerEvent.TeilfahrtID,
                                 lkwPlatzLeerEvent.LkwPlatzPositionID,
                                 result.Reason);
                              SetState(ProcessState.FahrtAbgeschlossen);
                              SetState(ProcessState.Idle);
                           }
                           else if (string.Equals(
                              result.AuftragsTyp,
                              "EINLAGERN_BEREITS_FERTIG",
                              StringComparison.OrdinalIgnoreCase))
                           {
                              _logger.LogInformation(
                                 "01BE|Event_207 gehoert zu einem bereits abgeschlossenen Einlagerauftrag und wurde ohne Zustandsaenderung ignoriert: Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, Grund={Reason}.",
                                 lkwPlatzLeerEvent.AuftragsNummer,
                                 lkwPlatzLeerEvent.TeilfahrtID,
                                 result.Reason);
                           }
                           else
                           {
                              _logger.LogWarning(
                                 "01B5|Event_207 hat keinen offenen Einlagerauftrag beendet: Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, LkwPlatz={LkwPlatz}, Grund={Reason}.",
                                 lkwPlatzLeerEvent.AuftragsNummer,
                                 lkwPlatzLeerEvent.TeilfahrtID,
                                 lkwPlatzLeerEvent.LkwPlatzPositionID,
                                 result.Reason);
                           }
                        }

                        // 1. Datenfluss zur SPS sicherstellen
                        await _opcClientCrane.EnsureDataFlowAsync(stoppingToken);

                        // Optionale Ueberwachungsausgabe
                        //LogOpenCraneQueueOrdersIfChanged();
                     }
                     catch (OperationCanceledException)
                     {
                        throw;
                     }
                     catch (Exception ex)
                     {
                        _logger.LogError(ex, "0035|Fehler im State-Machine-Zyklus (z.B. OPC-Verbindungsverlust). Zustand war: {state}", _currentState);

                        SetState(ProcessState.Fehler);

                        // Dem System im Fehlerfall etwas Zeit zum Atmen geben
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                        // Da ein Fehler auftrat, brechen wir die innere TryRead-Schleife ab,
                        // um den Datenfluss im naechsten Hauptdurchlauf frisch zu pruefen.
                        break;
                     }
                  }
               }
            }
            finally
            {
               watchdogTimerCancellation.Cancel();

               try
               {
                  await watchdogTimer;
               }
               catch (OperationCanceledException)
               {
               }

               try
               {
                  await opcDataFlowMonitor;
               }
               catch (OperationCanceledException)
               {
               }

               try
               {
                  await spsResendMonitor;
               }
               catch (OperationCanceledException)
               {
               }

               _logger.LogInformation("0038|Disconnecting from OPC Server...");
               _opcClientCrane.Disconnect();
            }
         }
         catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
         {
            _logger.LogInformation("0036|State machine stopping via CancellationToken.");
         }
         catch (Exception ex)
         {
            _logger.LogCritical(ex, "0037|Schwerwiegender Fehler ausserhalb der Hauptschleife. Dienst wird beendet!");
         }
      }

      private void LogWatchdogSummaryIfDue(int currentLebensZaehler)
      {
         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextWatchdogSummaryUtc)
         {
            return;
         }

         _logger.LogInformation(
            "0043|Watchdog aktiv. In den letzten 60 Sekunden wurden {WatchdogCount} Watchdog-Events verarbeitet. Aktueller LebensZaehler={LebensZaehler}.",
            watchdogEventsInCurrentMinute,
            currentLebensZaehler);

         watchdogEventsInCurrentMinute = 0;
         nextWatchdogSummaryUtc = nowUtc.AddMinutes(1);
      }

      private void InitializeStateFromDatabase()
      {
         AktuelleFahrtResult aktuelleFahrt =
            _aktuelleFahrtRepository.GetAktuelleFahrt();

         if (aktuelleFahrt.Success && aktuelleFahrt.AktuelleFahrtID is not null)
         {
            _runtimeStatus.SetAktuelleFahrt(aktuelleFahrt);

            _logger.LogInformation(
               "0054|Programmstart: Aktuelle Fahrt aus Datenbank erkannt. Rekonstruiere Zustand aus FALCOM_AKTUELLE_FAHRT. AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Typ={AuftragsTyp}, QuellePositionID={QuellePositionID}, QuelleUnterposition={QuelleUnterposition}, ZielPositionID={ZielPositionID}, ZielUnterposition={ZielUnterposition}, Quelle={Quelle}, Ziel={Ziel}, SollMengeKg={SollMengeKg}, SpsSendestatus={SpsSendestatus}.",
               aktuelleFahrt.AktuelleFahrtID,
               aktuelleFahrt.AuftragID,
               aktuelleFahrt.AuftragsTyp,
               aktuelleFahrt.QuellePositionID,
               aktuelleFahrt.QuelleUnterposition,
               aktuelleFahrt.ZielPositionID,
               aktuelleFahrt.ZielUnterposition,
               aktuelleFahrt.Quelle,
               aktuelleFahrt.Ziel,
               aktuelleFahrt.SollMengeKg,
               aktuelleFahrt.SpsSendestatus);

            if (string.Equals(aktuelleFahrt.SpsSendestatus, "FEHLER", StringComparison.OrdinalIgnoreCase))
            {
               _logger.LogWarning(
                  "0111|Programmstart: Aktuelle Fahrt hat einen technischen SPS-Sendefehler. Sie bleibt aktiv und wird automatisch erneut gesendet. AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Teilfahrt={AuftragTeilfahrt}, Grund={Grund}.",
                  aktuelleFahrt.AktuelleFahrtID,
                  aktuelleFahrt.AuftragID,
                  aktuelleFahrt.AuftragTeilfahrt,
                  aktuelleFahrt.SpsSendefehler);
               SetState(ProcessState.OpcGestoert);
            }
            else
            {
               SetState(ProcessState.WarteAufSpsRueckmeldung);
            }
            return;
         }

         _logger.LogInformation(
            "0055|Programmstart: Keine aktuelle Fahrt in FALCOM_AKTUELLE_FAHRT gefunden. Zustand Idle.");
         SetState(ProcessState.Idle);
      }

      private void SetState(ProcessState nextState)
      {
         _currentState = nextState;

         if (_lastLoggedState == nextState)
         {
            return;
         }

         _logger.LogInformation(
            "002F|Zustandswechsel: {previousState} -> {currentState}",
            FormatProcessState(_lastLoggedState),
            FormatProcessState(nextState));

         _lastLoggedState = nextState;
      }

      private static string FormatProcessState(ProcessState? state)
      {
         if (state is null)
         {
            return "INITIAL";
         }

         return state.Value switch
         {
            ProcessState.Fehler => "Störung",
            ProcessState.OpcGestoert => "OPC_GESTOERT",
            _ => state.Value.ToString()
         };
      }

      private async Task ScheduleWatchdogEventsAsync(CancellationToken stoppingToken)
      {
         using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

         while (await timer.WaitForNextTickAsync(stoppingToken))
         {
            if (Interlocked.CompareExchange(
               ref watchdogEventPending,
               1,
               0) != 0)
            {
               continue;
            }

            await _eventQueue.Writer.WriteAsync(
               new WatchdogEvent(watchdogValue),
               stoppingToken);

            watchdogValue = watchdogValue == int.MaxValue
               ? 0
               : watchdogValue + 1;
         }
      }

      private async Task ScheduleOpcDataFlowChecksAsync(CancellationToken stoppingToken)
      {
         using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

         while (await timer.WaitForNextTickAsync(stoppingToken))
         {
            try
            {
               await _opcClientCrane.EnsureDataFlowAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
               throw;
            }
            catch (Exception ex)
            {
               _logger.LogWarning(
                  "0060|Zyklische OPC-Datenflusspruefung fehlgeschlagen. Fehler={ExceptionType}: {Message}",
                  ex.GetType().Name,
                  ex.Message);
            }
         }
      }


      private async Task ScheduleSpsResendChecksAsync(CancellationToken stoppingToken)
      {
         using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

         while (await timer.WaitForNextTickAsync(stoppingToken))
         {
            try
            {
               AktuelleFahrtResult aktuelleFahrt =
                  _aktuelleFahrtRepository.TryClaimSpsResend();

               if (!aktuelleFahrt.Success || aktuelleFahrt.AktuelleFahrtID is null)
               {
                  continue;
               }

               _runtimeStatus.SetAktuelleFahrt(aktuelleFahrt);

               _logger.LogInformation(
                  "0087|SPS-Sendeversuch fuer aktuelle Fahrt wird gestartet. AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Teilfahrt={AuftragTeilfahrt}, QuelleUnterposition={QuelleUnterposition}, ZielUnterposition={ZielUnterposition}, Grund={Grund}, AngefordertVon={AngefordertVon}.",
                  aktuelleFahrt.AktuelleFahrtID,
                  aktuelleFahrt.AuftragID,
                  aktuelleFahrt.AuftragTeilfahrt,
                  aktuelleFahrt.QuelleUnterposition,
                  aktuelleFahrt.ZielUnterposition,
                  aktuelleFahrt.SpsSendewunschGrund,
                  aktuelleFahrt.SpsSendewunschVon);

               KranfahrtAuftragEvent kranfahrtAuftragEvent =
                  KranfahrtAuftragEvent.FromAktuelleFahrt(
                     aktuelleFahrt,
                     auftragTeilfahrt: aktuelleFahrt.AuftragTeilfahrt ?? 1,
                     toleranzKg: 150m);

               OPC_Client_Crane.OpcSendResult sendResult =
                  await _opcClientCrane.SendKranfahrtAuftragAsync(
                     kranfahrtAuftragEvent,
                     stoppingToken);

               if (sendResult.Success)
               {
                  AktuelleFahrtResult sentResult =
                     _aktuelleFahrtRepository.MarkSpsSendSuccess(
                        aktuelleFahrt.AktuelleFahrtID,
                        sendResult.TelegrammNummer,
                        sendResult.ZaehlerAnfahrt);

                  _runtimeStatus.SetAktuelleFahrt(sentResult);

                  _logger.LogInformation(
                     "0088|Aktuelle Fahrt wurde erneut an die Kran-SPS gesendet: AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Teilfahrt={AuftragTeilfahrt}, TelegrammNummer={TelegrammNummer}.",
                     sentResult.AktuelleFahrtID,
                     sentResult.AuftragID,
                     sentResult.AuftragTeilfahrt,
                     sendResult.TelegrammNummer);

                  SetState(ProcessState.FahrtAnSpsGesendet);
                  SetState(ProcessState.WarteAufSpsRueckmeldung);
                  continue;
               }

               AktuelleFahrtResult failedResult =
                  _aktuelleFahrtRepository.MarkSpsSendFailure(
                     aktuelleFahrt.AktuelleFahrtID,
                     sendResult.Reason);

               _runtimeStatus.SetAktuelleFahrt(failedResult);

               _logger.LogWarning(
                  "0089|SPS-Sendeversuch ist erneut technisch fehlgeschlagen. Aktuelle Fahrt bleibt aktiv und wird weiter automatisch versucht: AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Grund={Reason}.",
                  failedResult.AktuelleFahrtID,
                  failedResult.AuftragID,
                  sendResult.Reason);

               SetState(ProcessState.OpcGestoert);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
               throw;
            }
            catch (Exception ex)
            {
               _logger.LogWarning(
                  "008B|Pruefung auf SPS-Neusendeanforderung fehlgeschlagen. Fehler={ExceptionType}: {Message}",
                  ex.GetType().Name,
                  ex.Message);
            }
         }
      }

      public override async Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("0039|Windows-Dienst Stop angefordert.");
         await base.StopAsync(cancellationToken);
      }

      
   }
}






