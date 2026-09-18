/** Anexos de lançamento e de parcelamento (docs/06, docs/07). */
export interface Attachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
}

/**
 * Dono do anexo. O parcelamento é anexado no plano inteiro (a nota da compra vale para todas as
 * parcelas), e como o plano não guarda conta nem cartão, "parcelamento de conta" e "de cartão"
 * são o mesmo caso aqui.
 */
export type AttachmentOwnerKind = 'transaction' | 'installmentPlan';

export interface AttachmentOwner {
  kind: AttachmentOwnerKind;
  id: string;
}

/** Extensões aceitas — espelha a allowlist do FileSignatureValidator no backend. */
export const ATTACHMENT_ACCEPT = '.pdf,.jpg,.jpeg,.png,.webp,.heic,.heif';

export const ATTACHMENT_MAX_BYTES = 10 * 1024 * 1024;
