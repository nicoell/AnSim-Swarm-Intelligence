using System.Collections;
using System.Collections.Generic;
using AnSim.Runtime;
using UnityEngine;

public class SwarmBase : MonoBehaviour
{
  public int foodThreshold = 32;
  public Swarm kamikazeAttackSwarm;
  public Swarm enemySwarm;

  public GameObject explosionPrefab;

  private int _currentFood = 0;

  public void StoreFood(int food) { _currentFood += food; }

  // Update is called once per frame
  void Update()
  {
    if (_currentFood > foodThreshold)
    {
      _currentFood -= foodThreshold;
      kamikazeAttackSwarm.NumberOfSwarmsToRevive += 1;
    }
  }

  public void RemoteTriggerExplosion(Vector3 position, float explosionRadius)
  {
    kamikazeAttackSwarm.ActivateExplosion(position, explosionRadius * 10); //Increase the effect of explosion for kamikazeSwarm
    enemySwarm.ActivateExplosion(position, explosionRadius);
    var obj = Instantiate(explosionPrefab, position, Quaternion.identity);
    obj.GetComponent<Explosion>().explosionRadius = explosionRadius * 1.2f; //Make explosion animation a bit larger than actual explosion
  }
}
