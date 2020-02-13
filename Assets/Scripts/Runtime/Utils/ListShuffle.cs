
using System;
using System.Collections.Generic;

namespace AnSim.Runtime.Utils
{
  public static class ListShuffle
  {
    // List extension method for shuffling, taken from:
    // https://stackoverflow.com/a/1262619/4726335
    private static Random rng = new Random();

    public static void Shuffle<T>(this IList<T> list)
    {
      int n = list.Count;
      while (n > 1)
      {
        n--;
        int k = rng.Next(n + 1);
        T value = list[k];
        list[k] = list[n];
        list[n] = value;
      }
    }
  }
}
