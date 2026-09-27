namespace Pcb
{
    public enum PcbLayer { Front = 0, Back = 1 }

    public enum NodeType
    {
        Capacitor, // stop point
        Via,       // stop point that exists on both sides; the spark can flip side here
        Start,     // where the spark spawns
        Goal       // chip that ends the level
    }

    public static class PcbLayerExtensions
    {
        public static PcbLayer Other(this PcbLayer layer) =>
            layer == PcbLayer.Front ? PcbLayer.Back : PcbLayer.Front;
    }
}
