using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

public class MD5Generator : MonoBehaviour
{
    private void Start()
    {
        // string test_string = Path.Combine(Application.persistentDataPath, "718", "Lecture", "541_color.mp4");
        // Debug.Log(test_string);
        // Debug.Log(GetMD5HashFromFile(test_string));
    }
    public static string ProcessETag(string etag)
    {
        if (etag == null)
            return string.Empty;
        etag = etag.Replace("\"", "");
        etag = etag.Replace("/", "");
        etag = etag.ToUpper();
        return etag;
    }
    public static async Task<bool> CompareMD5(string fileName, string eTag)
    {
        Debug.Log(fileName + " "+ eTag);
        // if (string.Compare(GetMD5HashFile(fileName, 8), eTag, true) == 0)
        //     return true;
        // if (string.Compare(GetMD5HashFile(fileName, 16), eTag, true) == 0)
        //     return true;
        // if (string.Compare(GetMD5HashFile(fileName, 10000), eTag, true) == 0)
        //     return true;
        

        if (string.Compare(GetMD5Hash(fileName), eTag, true) == 0)
        {
            Debug.Log("Etags matched " + GetMD5Hash(fileName) + " " + eTag);
            return true;
        }
        if (string.Compare(await GetMD5HashFile(fileName, 8), eTag, true) == 0)
        {
            var hash = await GetMD5HashFile(fileName, 8);
            Debug.Log("Etags matched " + hash + " " + eTag);   
            return true;
        }
        // if (string.Compare(await GetMD5HashFile(fileName, 16), eTag, true) == 0)
        // {
        //     var hash = await GetMD5HashFile(fileName, 16);
        //     Debug.Log("Etags matched " + hash + " " + eTag);   
        //     return true;
        // }
        Debug.LogError("Etags unmatched");
        return false;
    }
    public static string GetMD5Hash(string fileName)
    {
        Debug.Log("checking md5sum normal");
        byte[] bytes;
        using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
            using (MD5 md5 = MD5.Create())
            {
                bytes = md5.ComputeHash(fileStream);
                Debug.Log(bytes.Length);
                Debug.Log(" md5sum  -  " + BitConverter.ToString(bytes).Replace("-", "").ToLower());
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }
    }
    
    
    public static async Task<string> GetMD5HashFile(string fileName, int size)
    {
        Debug.Log("checking md5 chunk - " + fileName);
        using (var md5 = MD5.Create())
        {
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(fileName);
                IEnumerable<byte> concatHash = new byte[] { };
                int concatCount = 0;
                long fileSize = new FileInfo(fileName).Length;
                for (int chunkAgg = 0; chunkAgg < fileSize; chunkAgg += size * 1024 * 1024)
                {
                    if (fileSize < size * 1024 * 1024)
                    {
                        byte[] hash = md5.ComputeHash(bytes);
                        return BitConverter.ToString(hash).Replace("-", String.Empty);
                    }
                    else if (fileSize - chunkAgg < size * 1024 * 1024)
                    {
                        byte[] hash = md5.ComputeHash(bytes, (int)chunkAgg, (int)(fileSize - chunkAgg));
                        concatHash = concatHash.Concat(hash);
                    }
                    else
                    {
                        byte[] hash = md5.ComputeHash(bytes, (int)chunkAgg, size * 1024 * 1024);
                        concatHash = concatHash.Concat(hash);
                    }
                    concatCount++;
                }
                if (concatCount == 1)
                    return BitConverter.ToString(md5.ComputeHash(concatHash.ToArray())).Replace("-", string.Empty);
                else
                    return BitConverter.ToString(md5.ComputeHash(concatHash.ToArray())).Replace("-", string.Empty) +
                           "-" + concatCount;
            }
            catch ( Exception e)
            {
                Debug.LogError(e.ToString());
                return "";
            }
        
        }
    }
    public string MatchMD5(string filename, string eTag)
    {
        return "";
    }

}