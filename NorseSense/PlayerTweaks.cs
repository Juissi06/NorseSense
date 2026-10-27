namespace NorseSense
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
            if (player == null || Compat.PlayerTweaksDisabled || !Plugin.ModEnabled.Value)
            {
                // Hand the player back with the values it had before we touched it.
                if (player != null && player == _player)
                {
                    player.m_maxPlaceDistance = _origPlaceDistance;
                    player.m_autoPickupRange = _origPickupRange;
                }
                _player = null;
                return;
            }

            if (player != _player)
            {
                _player = player;
                _origPlaceDistance = player.m_maxPlaceDistance;
                _origPickupRange = player.m_autoPickupRange;
            }

            float build = ServerLimits.MaxOrVanilla(Plugin.BuildDistance), pickup = ServerLimits.MaxOrVanilla(Plugin.PickupRange);
            player.m_maxPlaceDistance = build > 0f ? build : _origPlaceDistance;
            player.m_autoPickupRange = pickup > 0f ? pickup : _origPickupRange;
        }
    }
}
