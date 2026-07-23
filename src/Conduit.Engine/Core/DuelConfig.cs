namespace Conduit.Engine.Core
{
    /// <summary>
    /// Every tunable constant (spec §2). The engine hardcodes nothing here —
    /// Conduit.Content loads these from content/config/balance.v0.json.
    /// All energy values are milli-energy (1 energy = 1000). All times are ticks (20 Hz).
    /// </summary>
    public sealed record DuelConfig
    {
        public int TicksPerMinute { get; init; } = 1200;          // 20 Hz
        public int LifeMax { get; init; } = 150;
        public int EnergyCapMilli { get; init; } = 10000;
        public int StartingEnergyMilli { get; init; } = 3000;
        public int BaseRegenMilliPerMin { get; init; } = 20000;
        public int RampMilliPerMinPerMin { get; init; } = 600;
        public int RampCapMilliPerMin { get; init; } = 26000;
        public int SurgeBp { get; init; } = 15000;                // regen × while outnumbered
        public int AttackPeriodTicks { get; init; } = 60;
        public int SwiftAttackPeriodTicks { get; init; } = 40;
        public int[] CastDurationTicks { get; init; } = { 15, 25, 50, 80 };      // Sign..Ritual
        public int[] CastTierBp { get; init; } = { 10000, 13000, 17000, 22000 }; // Sign..Ritual
        public int SchoolCooldownTicks { get; init; } = 160;
        public int EchoWindowTicks { get; init; } = 300;
        public int EchoFirstBp { get; init; } = 7000;
        public int EchoSecondBp { get; init; } = 5000;
        public int ChannelRefundBp { get; init; } = 7500;         // on cancel/interrupt
        public int GuardCostMilli { get; init; } = 500;
        public int GuardChannelTicks { get; init; } = 5;
        public int GuardActiveTicks { get; init; } = 20;
        public int GuardBaseBp { get; init; } = 3000;
        public int GuardBpPerMissingLife { get; init; } = 25;
        public int GuardCapBp { get; init; } = 5500;
        public int DrawIntervalTicks { get; init; } = 600;
        public int DrawChargeCap { get; init; } = 2;
        public int OpeningHandSize { get; init; } = 5;
        public int BoardCap { get; init; } = 4;
        public int DeckSize { get; init; } = 30;
        public int RegrowIntervalTicks { get; init; } = 100;
        public int HashIntervalTicks { get; init; } = 100;
        public int AmbushRevealBp { get; init; } = 20000;

        public static DuelConfig Default { get; } = new DuelConfig();
    }
}
