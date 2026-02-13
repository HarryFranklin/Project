// for X and Y
public enum AxisVariable
{
    LifeSatisfaction,   // 0-10
    PersonalUtility,    // 0-1 (uSelf)
    SocietalFairness,   // 0-1 
    Wealth,              // 0-10 (For later expansion)

    // Delta metrics:
    DeltaPersonalUtility,
    DeltaSocietalFairness,

    // Histogram view
    Stack
}

// for Z (face)
public enum FaceMode
{
    Split, // Left = Self, Right = Society
    PersonalWellbeing, // Entire face = self
    SocietalFairness, // Entire face = society
    Cluster
}