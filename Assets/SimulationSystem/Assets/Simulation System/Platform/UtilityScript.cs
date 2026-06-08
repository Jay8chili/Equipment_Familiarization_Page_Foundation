using System.Collections;
using System;
using UnityEngine;
using System.IO;

public class UtilityScript : MonoBehaviour
{
    public static Sprite TextureToSprite(Texture2D tex)
    {
        Sprite new_sprite;
        new_sprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
        return new_sprite;
    }
    
    public static Sprite LoadImage(string filePath)
    {
        print(filePath);
        Texture2D texture = null;
        byte[] fileData;
            
        if ( File.Exists(filePath) )
        {
            fileData = File.ReadAllBytes(filePath);
            texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            
            Sprite sprite = Sprite.Create( texture, new Rect(0, 0, texture.width, texture.height), new Vector2(texture.width/2, texture.height/2) );
            print("file exists");
            return sprite;
        }

        return null;
    }
    
    
}
