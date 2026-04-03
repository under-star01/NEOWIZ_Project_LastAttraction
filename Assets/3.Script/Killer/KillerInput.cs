using UnityEngine;

public class KillerInput : MonoBehaviour
{
    public Vector2 Move => TestMng.inputSys?.Killer.Move.ReadValue<Vector2>() ?? Vector2.zero;
    public Vector2 Look => TestMng.inputSys?.Killer.Look.ReadValue<Vector2>() ?? Vector2.zero;
    public bool IsAttackPressed => TestMng.inputSys?.Killer.Attack.IsPressed() ?? false;
    // 상호작용 키 (예: Space 또는 R)
    //public bool IsInteracting => TestMng.inputSys?.Killer.Interact.IsPressed() ?? false;
}