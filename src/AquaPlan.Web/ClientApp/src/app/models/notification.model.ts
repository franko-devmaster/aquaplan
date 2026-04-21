// AQ-43 — Notification center model (in-app bell + dropdown).

export enum NotificationType {
  OrderAssigned = 'OrderAssigned',
  RoundAssigned = 'RoundAssigned',
  ResultsReceived = 'ResultsReceived',
  NonConformResult = 'NonConformResult',
}

export interface NotificationDto {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  isRead: boolean;
  isUrgent: boolean;
  createdAt: string;
  readAt: string | null;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
}

export interface NotificationListDto {
  items: NotificationDto[];
  unreadCount: number;
  urgentUnreadCount: number;
  totalCount: number;
}

export interface NotificationLogDto {
  id: string;
  notificationId: string;
  toEmail: string;
  subject: string;
  body: string;
  wasActuallySent: boolean;
  createdAt: string;
}
