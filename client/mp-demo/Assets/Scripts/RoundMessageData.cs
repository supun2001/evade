using System;

[Serializable]
public class RoundPhaseMessageData
{
    public string phase;
    public int roundIndex;
    public int timeRemainingMs;
    public int roundDurationMs;
    public int intermissionDurationMs;
    public bool isMapVoteOpen;
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

[Serializable]
public class MapVoteCandidateMessageData
{
    public string mapId;
    public string sceneName;
    public string displayName;
    public string difficulty;
}

[Serializable]
public class MapVoteCountMessageData
{
    public string mapId;
    public int count;
}

[Serializable]
public class MapVoteStateMessageData
{
    public bool isOpen;
    public int timeRemainingMs;
    public string selectedMapId;
    public MapVoteCandidateMessageData[] candidates;
    public MapVoteCountMessageData[] votes;
}

[Serializable]
public class MapSelectedMessageData
{
    public string mapId;
    public string sceneName;
    public string displayName;
    public string difficulty;
    public bool isInitial;
}
