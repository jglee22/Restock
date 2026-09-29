using UnityEngine;

public class PlacedFacility : MonoBehaviour
{
    FacilityDefinition definition;
    Vector2Int gridOrigin;

    public FacilityDefinition Definition => definition;
    public Vector2Int GridOrigin => gridOrigin;

    public void Initialize(FacilityDefinition source, Vector2Int origin)
    {
        definition = source;
        gridOrigin = origin;
    }
}
