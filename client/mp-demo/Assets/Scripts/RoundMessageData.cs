using System;

[Serializable]
public class RoundPhaseMessageData
{
    public string phase;
    public int roundIndex;
    public int timeRemainingMs;
    public int roundDurationMs;
    public int intermissionDurationMs;
}

[Serializable]
public class RoundAnnouncementMessageData
{
    public string title;
    public string subtitle;
    public float durationSeconds;
}

[Serializable]
public class RoundResultEntryMessageData
{
    public string sessionId;
    public string displayName;
    public int bestTimeMs;
    public int downedCount;
    public int revivesDone;
    public int joinOrder;
    public int rank;
}

[Serializable]
public class RoundResultsMessageData
{
    public int roundIndex;
    public int roundDurationMs;
    public RoundResultEntryMessageData[] entries;
}

[Serializable]
public class RoundPlayerResetMessageData
{
    public float x;
    public float y;
    public float z;
    public float rotationY;
}
