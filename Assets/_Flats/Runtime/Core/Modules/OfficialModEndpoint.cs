namespace Flats.Modules
{
    // Optional public HTTPS catalogue. Local modules work without a catalogue.
    public static class OfficialModEndpoint
    {
        public static string Url => System.Environment.GetEnvironmentVariable("FLATS_MOD_CATALOGUE_URL") ?? "";
    }
}
