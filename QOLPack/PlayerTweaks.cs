namespace QolPack
{
    // Config value 0 means "leave vanilla", so the original values are remembered per player instance.
    static class PlayerTweaks
    {
        static Player _player;
        static float _origPlaceDistance;
        static float _origPickupRange;

        internal static void Update()
        {
            if (Player.m_localPlayer != _player) Apply();
        }

        internal static void Apply()
        {
            var player = Player.m_localPlayer;
            if (player == null || Compat.PlayerTweaksDisabled)
            {
                _player = null;
                return;
            }

            if (player != _player)
            {
                _player = player;
                _origPlaceDistance = player.m_maxPlaceDistance;
                _origPickupRange = player.m_autoPickupRange;
            }

            player.m_maxPlaceDistance = Plugin.BuildDistance.Value > 0f ? Plugin.BuildDistance.Value : _origPlaceDistance;
            player.m_autoPickupRange = Plugin.PickupRange.Value > 0f ? Plugin.PickupRange.Value : _origPickupRange;
        }
    }
}
