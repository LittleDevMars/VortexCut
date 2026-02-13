namespace VortexCut.Core.Interfaces;

/// <summary>
/// Undo/Redo 가능한 편집 액션 인터페이스
/// </summary>
public interface IUndoableAction
{
    /// <summary>
    /// 액션 설명 (Undo/Redo 메뉴 표시용)
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 액션 실행
    /// </summary>
    void Execute();

    /// <summary>
    /// 액션 되돌리기
    /// </summary>
    void Undo();
}
