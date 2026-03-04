-- Create missing schemas and tables for chat and notifications services

CREATE SCHEMA IF NOT EXISTS chat_service;
CREATE SCHEMA IF NOT EXISTS notifications_service;

-- ── chat_service tables ────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS chat_service.conversations (
    "Id"                    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    "Type"                  VARCHAR(20)  NOT NULL,
    "ClientOrChildId"       UUID         NOT NULL,
    "BankerOrParentId"      UUID,
    "Label"                 VARCHAR(200) NOT NULL,
    "Status"                VARCHAR(20)  NOT NULL DEFAULT 'Active',
    "ClosedAt"              TIMESTAMPTZ,
    "InternalNotes"         VARCHAR(4000),
    "LastClientMessageAt"   TIMESTAMPTZ,
    "LastBankerMessageAt"   TIMESTAMPTZ,
    "CreatedAt"             TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS chat_service.messages (
    "Id"             UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    "ConversationId" UUID          NOT NULL REFERENCES chat_service.conversations("Id") ON DELETE CASCADE,
    "SenderId"       UUID          NOT NULL,
    "SenderName"     VARCHAR(200)  NOT NULL,
    "Content"        VARCHAR(2000) NOT NULL,
    "SentAt"         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "IsSystem"       BOOLEAN       NOT NULL DEFAULT FALSE,
    "ReadAt"         TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS chat_service.attachments (
    "Id"          UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    "MessageId"   UUID          NOT NULL REFERENCES chat_service.messages("Id") ON DELETE CASCADE,
    "FileName"    VARCHAR(500)  NOT NULL,
    "ContentType" VARCHAR(100)  NOT NULL,
    "FileSize"    BIGINT        NOT NULL,
    "StoragePath" VARCHAR(1000) NOT NULL,
    "CreatedAt"   TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

-- ── notifications_service tables ──────────────────────────────────
CREATE TABLE IF NOT EXISTS notifications_service.notifications (
    "Id"                UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId"            UUID          NOT NULL,
    "Title"             VARCHAR(200)  NOT NULL,
    "Message"           VARCHAR(2000) NOT NULL,
    "Type"              VARCHAR(20)   NOT NULL,
    "Priority"          VARCHAR(20)   NOT NULL,
    "Channel"           VARCHAR(20)   NOT NULL,
    "Status"            VARCHAR(20)   NOT NULL DEFAULT 'Pending',
    "RelatedEntityType" VARCHAR(100),
    "RelatedEntityId"   UUID,
    "CreatedAt"         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "ReadAt"            TIMESTAMPTZ,
    "SentAt"            TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS "IX_notifications_UserId"
    ON notifications_service.notifications("UserId");

CREATE INDEX IF NOT EXISTS "IX_notifications_UserId_Status"
    ON notifications_service.notifications("UserId", "Status");

CREATE TABLE IF NOT EXISTS notifications_service.notification_preferences (
    "Id"                        UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId"                    UUID    NOT NULL,
    "TransactionNotifications"  BOOLEAN NOT NULL DEFAULT TRUE,
    "SecurityNotifications"     BOOLEAN NOT NULL DEFAULT TRUE,
    "CardNotifications"         BOOLEAN NOT NULL DEFAULT TRUE,
    "LimitNotifications"        BOOLEAN NOT NULL DEFAULT TRUE,
    "ChatNotifications"         BOOLEAN NOT NULL DEFAULT TRUE,
    "EmailNotificationsEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "PushNotificationsEnabled"  BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT "UQ_notification_preferences_UserId" UNIQUE ("UserId")
);
