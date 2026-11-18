namespace MonopolyGame.Debugging
{
    /// <summary>Local commands only print text; server commands must run on the host.</summary>
    public enum DebugCommandExecutionMode
    {
        Local,
        Server
    }
}
