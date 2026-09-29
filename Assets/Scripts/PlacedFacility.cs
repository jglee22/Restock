using UnityEngine;

public class PlacedFacility : MonoBehaviour
{
    FacilityDefinition definition;
    Vector2Int gridOrigin;
    int rotationQuarterTurns;

    public FacilityDefinition Definition => definition;
    public Vector2Int GridOrigin => gridOrigin;
    public int RotationQuarterTurns => rotationQuarterTurns;

    public void Initialize(FacilityDefinition source, Vector2Int origin, int quarterTurns)
    {
        definition = source;
        UpdatePlacement(origin, quarterTurns);
    }

    public void UpdatePlacement(Vector2Int origin, int quarterTurns)
    {
        gridOrigin = origin;
        rotationQuarterTurns = NormalizeQuarterTurns(quarterTurns);
    }

    public static int NormalizeQuarterTurns(int quarterTurns)
    {
        return ((quarterTurns % 4) + 4) % 4;
    }
}
