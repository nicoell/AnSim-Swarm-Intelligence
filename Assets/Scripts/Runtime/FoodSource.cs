using System.Collections;
using System.Collections.Generic;
using AnSim.Runtime;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class FoodSource : MonoBehaviour
{
  public SwarmSimManager swarmSimManager;
  public int minFoodAmount = 1;
  public int maxFoodAmount = 16;

  private int _foodAmount;

  public int EatFood()
  {
    var edibleFood = _foodAmount;
    _foodAmount = 0;
    return edibleFood;
  }

  // Update is called once per frame
  void Update()
  {
    if (_foodAmount == 0)
    {
      transform.position = swarmSimManager.GetValidFoodPosition();
      _foodAmount = Random.Range(minFoodAmount, maxFoodAmount);
    }
  }
}
