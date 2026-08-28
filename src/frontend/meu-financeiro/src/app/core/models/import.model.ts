export interface ImportRowPreview {
  rowNumber: number;
  date?: string;
  description?: string;
  typeRaw?: string;
  accountOrCardRaw?: string;
  categoryRaw?: string;
  amount?: number;
  installmentRaw?: string;
  recurrenceRaw?: string;
  notes?: string;
  isValid: boolean;
  isPossibleDuplicate: boolean;
  errors: string[];
}

export interface ImportPreview {
  importFileId: string;
  originalFileName: string;
  alreadyImportedBefore: boolean;
  rowsTotal: number;
  rowsValid: number;
  rowsInvalid: number;
  rowsPossibleDuplicate: number;
  rows: ImportRowPreview[];
}

export interface ConfirmImportResult {
  importFileId: string;
  rowsImported: number;
  rowsRejected: number;
  resultSummary: string;
}

export interface ImportFileHistoryItem {
  id: string;
  originalFileName: string;
  status: string;
  createdAt: string;
  rowsImported?: number;
  rowsRejected?: number;
  resultSummary?: string;
}
