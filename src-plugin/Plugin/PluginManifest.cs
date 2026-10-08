namespace Alerena
{
    using CounterStrikeSharp.API.Core;

    public sealed partial class Plugin : BasePlugin
    {
        public override string ModuleName => "alerena";

        public override string ModuleDescription => "An arena plugin for Counter-Strike2";

        public override string ModuleAuthor => "ale-devl";

        public override string ModuleVersion => "3.0.0-rc.2 " +
#if RELEASE
            "(release)";
#else
            "(debug)";
#endif
    }
}