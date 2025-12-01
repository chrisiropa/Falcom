using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falcom
{
	public class Optimizer
	{
		private readonly Lager _lager;
		private readonly Parameter _parameter;
		private readonly Logging _logger;
		private string ziel_cu = "0.05";
		private string ziel_mn = "0.25";



		public Optimizer(Lager lager, Parameter parameter, Logging logger)
		{
			_lager = lager;
			_parameter = parameter;
			_logger = logger;
		}



		public void Run()
		{
			//Die Werte aus der Datenbank holen
			//double[] cu_values = { 0.084, 0.026, 0.014, 0.69, 0.037, 0.015, 0.016, 0.017, 0.5, 0.081, 0.017, 0.014, 0.015, 0.054, 0.5, 0.077 };
			//double[] mn_values = { 1.05, 0.431, 0.623, 1.633, 0.394, 0.18, 0.282, 0.5, 1.659, 0.157, 0.147, 0.217, 0.14, 0.407, 1.649, 0.22 };

			float[] cu_values = _lager.GetCuValues();
			float[] mn_values = _lager.GetMnValues();

			// Kombiniere alle Argumente zu einem String
			// string arguments = $"{ziel_cu} {ziel_mn}";
			// Array in einen String umwandeln (komma-separiert)
			string cu_values_string = string.Join(" ", cu_values).Replace(",", ".");
			string mn_values_string = string.Join(" ", mn_values).Replace(",", ".");

			// Argumente für das Python-Skript


			string arguments = $"{cu_values_string} {mn_values_string} {ziel_cu} {ziel_mn}";

			//Für die Überprüfung in Thonny
			string thonnyAufruf = "%Run Steel_Mixer.py " + arguments;

			_logger.ZLog(ELF.INFO, "THONNY");
			_logger.ZLog(ELF.INFO, thonnyAufruf);
			_logger.ZLog(ELF.INFO, "THONNY");

			ProcessStartInfo start = new ProcessStartInfo();
			//start.FileName = @"C:\Projekte\Hundhausen\Falcom\PythonFile\steel_mixer.exe";  
			start.FileName = _parameter.PythonExe;
			start.UseShellExecute = false;
			start.RedirectStandardOutput = true;
			start.RedirectStandardError = true;
			start.CreateNoWindow = true;
			start.Arguments = arguments;



			_logger.ZLog(ELF.INFO, "Start der Berechnung...");

			using (Process process = Process.Start(start))
			{
				using (StreamReader reader = process.StandardOutput)
				{
					string result = "";

					try
					{
						result = reader.ReadToEnd();

						_logger.ZLog(ELF.INFO, "Berechnung beendet");

						_logger.ZLog(ELF.INFO, "--------------RESULT----------------");
						_logger.ZLog(ELF.INFO, "\n{0}", result);

						BerechnungAuswerten(result);

					}
					catch (Exception e)
					{
						_logger.ZLog(ELF.ERROR, "{0}", e.Message);
					}


				}
			}
		}

		private void BerechnungAuswerten(string result)
		{
			// Splitte den Text nach Zeilen
			string[] lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			// Liste für die ersten 16 Werte (float)
			List<float> floatList = new List<float>();

			// Dictionary für die errechneten Cu- und Mn-Werte
			Dictionary<string, float> results = new Dictionary<string, float>();

			// Die ersten 16 Werte in die Liste aufnehmen
			for (int i = 0; i < 16; i++)
			{
				floatList.Add(float.Parse(lines[i].Replace(',', '.'), CultureInfo.InvariantCulture)); // Komma zu Punkt konvertieren
			}

			// Errechne Cu und Mn Werte aus dem Text auslesen
			results["Cu errechnet"] = float.Parse(lines[16], CultureInfo.InvariantCulture);
			results["Mn errechnet"] = float.Parse(lines[17], CultureInfo.InvariantCulture);

			float summeAnteile = 0.0f;
			foreach (var value in floatList)
			{
				summeAnteile += value;
			}
			_logger.ZLog(ELF.INFO, "Summe der Anteile = {0}", summeAnteile);


			//Nun die Gegenrechnung mit den Lageranalysen
			float[] cu_values = _lager.GetCuValues();
			float[] mn_values = _lager.GetMnValues();

			int platzNr = 1;
			float cu_value_GegenR = 0.0f;
			float mn_value_GegenR = 0.0f;

			foreach (var value in floatList)
			{
				if (value > 0)
				{
					float cu = cu_values[platzNr - 1];
					float mn = mn_values[platzNr - 1];

					// Ausgabe mit Formatierung
					_logger.ZLog(
						 ELF.INFO,
						 "Platz {0,-3} = {1,8:F4} %  cu = {2,8:F4} %  mn = {3,8:F4} %",
						 platzNr,
						 100.0f * value,
						 cu,
						 mn
					);

					cu_value_GegenR += value * cu;
					mn_value_GegenR += value * mn;
				}

				platzNr += 1;
			}

			_logger.ZLog(ELF.INFO, "Ziel Kupfer          = {0:F4} %", ziel_cu);
			_logger.ZLog(ELF.INFO, "Ziel Mangan          = {0:F4} %", ziel_mn);
			_logger.ZLog(ELF.INFO, "Gegenrechnung Kupfer = {0:F4} %", cu_value_GegenR);
			_logger.ZLog(ELF.INFO, "Gegenrechnung Mangan = {0:F4} %", mn_value_GegenR);

		}
	}
}