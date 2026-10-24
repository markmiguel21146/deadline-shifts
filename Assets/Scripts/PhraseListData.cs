using UnityEngine;

[CreateAssetMenu(fileName = "NewPhraseList", menuName = "Game/Phrase List")]
public class PhraseListData : ScriptableObject
{
    public TextAsset textFileToImport; // Drag Phrases.txt here
    public string[] phrases;

    [ContextMenu("Import Phrases From Text File")]
    public void ImportFromTextFile()
    {
        if (textFileToImport != null)
        {
            phrases = textFileToImport.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            Debug.Log($"Successfully imported {phrases.Length} phrases!");
        }
    }
}