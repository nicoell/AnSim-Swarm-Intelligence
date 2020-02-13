using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
  public AnimationCurve animationCurve;
  private float _timer = 0f;
  public float explosionDuration = 1f;
  public float explosionRadius = 1f;

  // Update is called once per frame
  void Update()
  {
    if (_timer > explosionDuration) Destroy(this);

    float scale = explosionRadius * animationCurve.Evaluate(_timer / explosionDuration);
    transform.localScale = Vector3.one * scale;
    _timer += Time.deltaTime;
  }
}
