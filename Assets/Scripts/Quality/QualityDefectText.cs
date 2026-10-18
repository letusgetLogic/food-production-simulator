using Game.Core;

namespace Game.Quality
{
    /// <summary>
    /// Display text of a defect code from <see cref="QualityInspector"/> (statistics page, debug panel).
    /// Keys: "defect.&lt;code&gt;" (defect.underweight), "defect.missing" for "&lt;Measurement&gt;Missing",
    /// details after the colon: "reject.&lt;reason&gt;" for Rejected:PressInterrupted and
    /// "product.&lt;state&gt;" for NotFinished:ToppedPizza.
    /// </summary>
    public static class QualityDefectText
    {
        /// <summary>"SauceLow" -> "Zu wenig Sauce", "Rejected:PressInterrupted" -> "Von Station aussortiert (Presshub abgebrochen)".</summary>
        public static string Get(string defect)
        {
            if (string.IsNullOrEmpty(defect))
            {
                return defect;
            }

            int colon = defect.IndexOf(':');
            if (colon <= 0)
            {
                return CodeLabel(defect);
            }

            string code = defect.Substring(0, colon);
            string detail = defect.Substring(colon + 1);
            string detailPrefix = code == QualityInspector.DefectRejected ? "reject."
                : code == QualityInspector.DefectNotFinished ? "product."
                : null;
            string detailText = detailPrefix != null ? LocText.Get(detailPrefix + LocText.Snake(detail), detail) : detail;
            return string.IsNullOrEmpty(detailText) ? CodeLabel(code) : $"{CodeLabel(code)} ({detailText})";
        }

        /// <summary>Code without detail: "WeightMissing" -> "Messwert fehlt: Gewicht".</summary>
        public static string CodeLabel(string code)
        {
            const string missing = QualityInspector.MissingSuffix;
            if (code.EndsWith(missing) && code.Length > missing.Length)
            {
                string measurement = code.Substring(0, code.Length - missing.Length);
                return LocText.Get("defect.missing", "Missing measurement") + ": " +
                       LocText.Get("defect." + LocText.Snake(measurement), measurement);
            }

            return LocText.Get("defect." + LocText.Snake(code), code);
        }
    }
}
