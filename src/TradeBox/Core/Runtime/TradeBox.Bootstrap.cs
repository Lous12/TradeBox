using NeoModLoader.api;

namespace Lous12.TradeBox
{
    public partial class Main : BasicMod<Main>
    {
        protected override void OnModLoad()
        {
            LogInfo(
                "TradeBox 0.1.0-dev10.7 loaded. " +
                "Economy keeps its own working scroll in a detached ScrollWindow overlay so other custom CityWindow tabs cannot capture it."
            );
        }
    }
}
