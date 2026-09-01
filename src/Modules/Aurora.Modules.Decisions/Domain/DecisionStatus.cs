namespace Aurora.Modules.Decisions.Domain;

public enum DecisionApprovalState { PendingApproval = 0, Approved = 1, Rejected = 2 }
public enum DecisionExecutionState { NotStarted = 0, InProgress = 1, Executed = 2, Failed = 3 }
