using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnSim.Runtime
{
  public class FoodLocation : MonoBehaviour
  {
    public MeshRenderer meshRenderer;
    public FoodManager foodManager;
    public int minFoodAmount = 1;
    public int maxFoodAmount = 16;
    public float respawnTime = 2.0f;
    private float _respawnTimer = 0.0f;
    private Vector3 _scale;

    public int EatFood()
    {
      meshRenderer.enabled = false;

      var edibleFood = _foodAmount;
      _foodAmount = 0;
      _respawnTimer = 0;
      return edibleFood;
    }

    private int _foodAmount;
    public bool IsActive { get; private set; } = true;

    public bool IsDepleted() { return _foodAmount == 0; }
    // Start is called before the first frame update
    void Awake()
    {
      _scale = transform.localScale;
      _foodAmount = Random.Range(minFoodAmount, maxFoodAmount);
      
      transform.localScale = _scale * _foodAmount / maxFoodAmount;

      meshRenderer = GetComponent<MeshRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
      if (!IsDepleted()) return;
      _respawnTimer += Time.deltaTime;

      if (_respawnTimer >= respawnTime)
      {
        meshRenderer.enabled = true;
        _foodAmount = Random.Range(minFoodAmount, maxFoodAmount);
        transform.localScale = _scale * _foodAmount / maxFoodAmount;
      }

    }

    private void OnDisable()
    {
      IsActive = false;
      foodManager.RemoveDisabledFoodLocations();
    }

    private void OnEnable()
    {
      IsActive = true;
      foodManager.RegisterFoodLocation(this);
    }

    private void OnDestroy()
    {
      IsActive = false;
      foodManager.RemoveDisabledFoodLocations();
    }
  }
}