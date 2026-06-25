using System;
using System.Collections.Generic;
using System.Text;

namespace Falcom
{
    public enum ProcessState
    {
        Idle,
        AuftragBereit,
        FahrtAnSpsGesendet,
        WarteAufSpsRueckmeldung,
        FahrtAbgeschlossen,
        Fehler
   }
}
