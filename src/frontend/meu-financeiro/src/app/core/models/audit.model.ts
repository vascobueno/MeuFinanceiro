export interface AuditLogEntry {
  id: string;
  timestamp: string;
  userId: string | null;
  userName: string | null;
  action: string;
  entityName: string;
  entityId: string;
  ipAddress: string | null;
}

export interface AuditLogFilter {
  startDate: string | null;
  endDate: string | null;
  action: string | null;
  page: number;
  pageSize: number;
}
