namespace Formicarium.Core.Actuation
{
    /// <summary>An 8-bit-per-channel colour for one riser-mouth ring.</summary>
    public struct RgbColor
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public RgbColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static RgbColor Black
        {
            get { return new RgbColor(0, 0, 0); }
        }

        public bool IsBlack
        {
            get { return R == 0 && G == 0 && B == 0; }
        }
    }

    /// <summary>
    /// Everything the controller can switch. Kept as an interface with no hardware types in
    /// its signature so the whole control loop can be exercised against a fake on a desktop.
    ///
    /// There is deliberately no gate here. A powered gate between the nest and its food would
    /// be the largest single point of failure in the system: a servo that jams closed starves
    /// the colony, unattended, for as long as it takes someone to notice. The three risers are
    /// always open and redundant against one fouling, and service closure is a manual plug cap
    /// that cannot fail closed while nobody is watching.
    /// </summary>
    public interface IActuatorSet
    {
        void SetHeater(bool on);

        /// <summary>
        /// Water into the hydration reservoir, which then wicks into the nest core. Deliberately
        /// not "into the nest": the reservoir is sized so its entire contents cannot harm the
        /// colony, which is what turns a stuck pump from a flood into a non-event.
        /// </summary>
        void SetRefillPump(bool on);

        /// <summary>Sugar water into the outworld dish. A separate pump and line: different fluids cannot share either.</summary>
        void SetFeedPump(bool on);

        /// <summary>Outworld circulation fan, in the lid.</summary>
        void SetFan(bool on);

        /// <summary>
        /// Nest exhaust fan, in the electronics bay, pulling nest air through a mesh-screened
        /// ceiling port. A mould guard rather than a climate control - the colony wants the nest
        /// humid, and the wicking core is continuously replacing what this removes.
        /// </summary>
        void SetNestFan(bool on);

        /// <summary>One colour per riser mouth, indexed by riser.</summary>
        void SetRiserLights(RgbColor[] colors);

        /// <summary>Fail-safe. Everything off, lights dark. Called on boot and on service mode.</summary>
        void AllOff();
    }
}
