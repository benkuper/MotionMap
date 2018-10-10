using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MMSelectableObject : MonoBehaviour
{
    public bool isOver;
    public bool isSelected;
    public float selectionProgression;

    public virtual void overChanged(bool isOver) { this.isOver = isOver;  }
    public virtual void selectionChanged(bool isSelected) { this.isSelected = isSelected; }
    public virtual void selectionProgress(float progress) { selectionProgression = progress;  }
}
