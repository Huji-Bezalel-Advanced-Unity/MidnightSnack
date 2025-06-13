// ButtonActionTester.cs (New File)
using UnityEngine;

/// <summary>
/// A simple script to test button actions.
/// Contains public methods that can be linked to a ButtonInteractable's UnityEvent.
/// </summary>
public class ButtonActionTester : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private string messageToPrint = "Button Test Action Executed!";
    [SerializeField] private int timesToPrint = 1;

    /// <summary>
    /// A public method designed to be called by a UnityEvent (like from ButtonInteractable).
    /// Prints a configurable message to the console.
    /// </summary>
    public void PrintTestMessage()
    {
        for (int i = 0; i < timesToPrint; i++)
        {
            Debug.Log($"<color=cyan>[ButtonActionTester] {messageToPrint} (from object: {gameObject.name})</color>");
        }
    }

    /// <summary>
    /// Another example public method.
    /// </summary>
    public void AnotherTestAction()
    {
        Debug.Log($"<color=yellow>[ButtonActionTester] Another Test Action was triggered on {gameObject.name}!</color>");
    }
}