using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RipperDoc : MonoBehaviour
{
    public List<MetaProgressionUI> metaProgressionUIs;
    public TMP_Text currencyText;

    private void Start()
    {
        LoadMetaProgressionFromInspector();
    }
    private void Update()
    {
        currencyText.text = GameManager.instance.saveSystem.GetCurrency().ToString();
    }
    public void LoadMetaProgressionFromInspector()
    {
        List<MetaProgressionContainer> metaProgressionContainers = GameManager.instance.saveSystem.GetMetaProgressionFromInspector();

        if (metaProgressionContainers != null)
        {
            for (int i = 0; i < metaProgressionContainers.Count; i++)
            {
                metaProgressionUIs[i].LoadMetaUI(metaProgressionContainers[i]);
            }
        }
        else
        {
            Debug.LogError("MetaProgressionContainers is null");
        }
    }


    public void SaveMetaProgressionToInspector()
    {
        List<MetaProgressionContainer> metaProgressionContainers = new List<MetaProgressionContainer>();

        for (int i = 0; i < metaProgressionUIs.Count; i++)
        {
            metaProgressionContainers.Add(metaProgressionUIs[i].metaProgressionContainer);
        }

        GameManager.instance.saveSystem.SaveMetaProgressionToInspector(metaProgressionContainers);
    }

    public void SaveMetaProgressionToFile()
    {
        // Save the meta progression data to a file
        GameManager.instance.saveSystem.SaveMetaProgression();
    }


}
