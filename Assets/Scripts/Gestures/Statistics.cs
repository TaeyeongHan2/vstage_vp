using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;

public enum GroupType
{
    Population,
    Sample
}

public static class Statistics
{
    public static float Average(List<float> values)
    {
        return values.Average();
    }

    public static float StandardDeviation(List<float> values, GroupType groupType = GroupType.Population)
    {
        float average = Average(values);
        
        float temp = 0;
        foreach (float value in values)
        {
            temp += (value - average) * (value - average);
        }
        
        int divisor = groupType == GroupType.Population ? values.Count : values.Count - 1;
        Assert.AreNotEqual(divisor, 0);
        
        float stDev = temp / divisor;
        
        return stDev;
    }
}