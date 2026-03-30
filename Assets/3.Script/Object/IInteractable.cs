public enum InteractType
{
    Hold,   // 좌클릭 유지형 (증거조사, 구출)
    Press   // SPACE 즉발형 (판자, 창틀)
}

public interface IInteractable
{
    InteractType InteractType { get; }

    void BeginInteract();
    void EndInteract();
}