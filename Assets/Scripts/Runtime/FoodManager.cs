using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnSim.Runtime.Utils;
using UnityEngine;

namespace AnSim.Runtime
{

  public class FoodManager : MonoBehaviour
  {
    private List<FoodLocation> _foodLocations;

    private void Awake()
    {
      _foodLocations = new List<FoodLocation>();
    }

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update() { }

    public FoodLocation RequestFoodLocation(Vector3 origin, float luckFactor)
    {
      _foodLocations.Shuffle();
      FoodLocation retLocation = null;
      float bestDistance = float.MaxValue;
      foreach (var foodLocation in _foodLocations)
      {
        if (foodLocation.IsDepleted()) continue;
        float distance = (foodLocation.transform.position - origin).magnitude;
        if (distance < bestDistance && (retLocation == null || Random.value <= luckFactor))
        {
          bestDistance = distance;
          retLocation = foodLocation;
        }
      }

      return retLocation;
    }

    public void RegisterFoodLocation(FoodLocation foodLocation)
    {
      if (_foodLocations != null)
      {
        if (_foodLocations.All(item => item.GetInstanceID() != foodLocation.GetInstanceID()))
        {
          _foodLocations.Add(foodLocation);
          Debug.Log("Registered FoodLocation: " + foodLocation.name);
        }
        else
        {
          Debug.Log("FoodLocationList already contains FoodLocation: " + foodLocation.name);
        }
      }
      else
      {
        Debug.Log("FoodLocation tried to register itself before FoodLocations List was created.");
        Debug.Log("Please change the Script Execution Order, so FoodManager comes before FoodLocation.");
      }

    }

    public void RemoveDisabledFoodLocations() { _foodLocations.RemoveAll(item => item == null || !item.IsActive); }
  }
}