using UnityEngine.InputSystem;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The key that opens and closes the debug tools. Not an action of a map: it is not a part of
    /// the game, and it is read whatever input is switched off, the map's under the city view included.
    /// </summary>
    public static class DebugKey
    {
        public const Key Toggle = Key.F1;

        public static bool WasPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;

            return keyboard != null && keyboard[Toggle].wasPressedThisFrame;
        }
    }
}
