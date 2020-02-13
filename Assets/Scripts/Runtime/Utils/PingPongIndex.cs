namespace AnSim.Runtime.Utils
{
  public class PingPongIndex
  {
    public void Advance() { Ping = Pong; }

    public uint Ping { get; private set; }
    public uint Pong => (Ping + 1) % 2;
  }
}