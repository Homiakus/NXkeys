using System;

namespace NXOpen
{
    public class NXException : Exception
    {
        public NXException() { }
        public NXException(string message) : base(message) { }
    }

    public class TaggedObject { }
    public class Part { }

    public sealed class PartCollection
    {
        public Part Work { get; set; }
        public Part Display { get; set; }
    }

    public sealed class ListingWindow
    {
        public void Open() { }
        public void WriteLine(string value) { }
    }

    public sealed class Session
    {
        public enum LibraryUnloadOption
        {
            AtTermination = 1
        }

        public ListingWindow ListingWindow { get; } = new ListingWindow();
        public PartCollection Parts { get; } = new PartCollection();
        public static Session GetSession() => new Session();
    }
}

namespace NXOpen.UF
{
    public sealed class UF
    {
        public void AskApplicationModule(out int moduleId)
        {
            moduleId = UFConstants.UF_APP_MODELING;
        }
    }

    public sealed class UFSession
    {
        public UF UF { get; } = new UF();
        public static UFSession GetUFSession() => new UFSession();
    }

    public static class UFConstants
    {
        public const int UF_APP_MODELING = 1;
        public const int UF_APP_DRAFTING = 2;
        public const int UF_APP_MANUFACTURING = 3;
        public const int UF_APP_SFEM = 4;
        public const int UF_APP_DESFEM = 5;
        public const int UF_APP_SHEETMETAL = 6;
        public const int UF_APP_ROUTING = 7;
        public const int UF_APP_STUDIO = 8;
    }
}
