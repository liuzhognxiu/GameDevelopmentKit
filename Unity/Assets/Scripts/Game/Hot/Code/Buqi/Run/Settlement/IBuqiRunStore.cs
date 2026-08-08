namespace Game.Hot.Buqi.Run.Settlement
{
    public interface IBuqiRunStore
    {
        bool TryRead(out string json, out string error);

        bool TryWrite(string json, out string error);

        bool TryDelete(out string error);
    }
}
