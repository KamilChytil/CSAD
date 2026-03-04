UPDATE identity_service.users
SET "IsEmailVerified" = true,
    "EmailVerificationToken" = NULL,
    "EmailVerificationTokenExpiresAt" = NULL
WHERE "IsEmailVerified" = false
  AND "EmailVerificationToken" IS NOT NULL;
