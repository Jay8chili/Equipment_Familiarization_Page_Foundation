using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SimulationDetails
{
    public int id;
    public int index;
    public string code;
    public string name;
    public string description;
    public string thumbnailURL;
    public string thumbnailEtag;
    public string androidAssetURL;
    public string androidAssetEtag;
    public string windowsAssetURL;
    public string windowsAssetEtag;
    
    public Sprite thumbnailSprite;
}
