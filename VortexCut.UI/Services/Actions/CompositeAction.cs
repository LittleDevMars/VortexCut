using VortexCut.Core.Interfaces;

namespace VortexCut.UI.Services.Actions;

/// <summary>
/// 복합 액션 (Razor 분할, Ripple 삭제 등 다중 작업을 하나의 Undo 단위로)
/// </summary>
public class CompositeAction : IUndoableAction
{
    private readonly List<IUndoableAction> _actions;

    public string Description { get; }

    public CompositeAction(string description, List<IUndoableAction> actions)
    {
        Description = description;
        _actions = actions;
    }

    /// <summary>
    /// 순서대로 실행
    /// </summary>
    public void Execute()
    {
        foreach (var action in _actions)
            action.Execute();
    }

    /// <summary>
    /// 역순으로 되돌리기
    /// </summary>
    public void Undo()
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
            _actions[i].Undo();
    }
}
