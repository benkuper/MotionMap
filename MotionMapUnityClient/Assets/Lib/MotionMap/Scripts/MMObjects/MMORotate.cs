using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MMORotate : MMSelectableObject
{
    public Vector3 overSpeed;
    public Vector3 selectionSpeed;
    public AnimationCurve overEvolution;

    void Update()
    {
        if (isSelected) transform.Rotate(selectionSpeed);
        else if (isOver) transform.Rotate(overEvolution.Evaluate(selectionProgression) * overSpeed);
    }
}
