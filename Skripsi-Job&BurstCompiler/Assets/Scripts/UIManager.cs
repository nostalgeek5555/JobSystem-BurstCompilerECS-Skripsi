using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    //dots properties

    // Start is called before the first frame update
    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        else
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
        }
    }



    // Update is called once per frame
    void Update()
    {
        
    }


    public enum Mode
    {
        HANDBUILT = 0,

    }
}
