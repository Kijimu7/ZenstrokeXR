using UnityEngine;
using UnityEngine.InputSystem;

public class TestCoinTrigger3D : MonoBehaviour
{
    [SerializeField] private WorldSpaceCoinFly3D coinSystem;

    private void Update()
    {
        if (coinSystem == null) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            coinSystem.PlayBurst();
        }
    }
}