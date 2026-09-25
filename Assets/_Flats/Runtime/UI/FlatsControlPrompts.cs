using System.Text.RegularExpressions;

public static class FlatsControlPrompts
{
    static readonly Regex token = new Regex(@"\{control:([A-Za-z]+)\}");
    public static string Resolve(string source)
    {
        if (string.IsNullOrEmpty(source) || !source.Contains("{control:")) return source;
        return token.Replace(source, match =>
        {
            string action = match.Groups[1].Value;
            bool pad = FlatsControls.UsingGamepad;
            if (action == "Move") return pad ? FlatsLocalization.Translate("Left stick") :
                FlatsControls.Label("Forward", false) + " / " + FlatsControls.Label("Left", false) + " / " + FlatsControls.Label("Backward", false) + " / " + FlatsControls.Label("Right", false);
            if (action == "Look") return FlatsLocalization.Translate(pad ? "Right stick" : "Mouse");
            if (action == "Menu") return pad ? "Start / Select" : "Esc";
            if (action == "Join") return "Enter / " + FlatsLocalization.Translate("Hold") + " " + FlatsControls.Label("Reload", true);
            if (pad && action == "Interact") return FlatsLocalization.Translate("Hold") + " " + FlatsControls.Label("Change", true);
            if ((pad || FlatsControls.HoldToAim) && action == "Aim") return FlatsLocalization.Translate("Hold") + " " + FlatsControls.Label(action, pad);
            return FlatsControls.Label(action, pad);
        });
    }
}
