-- The EF6 SQLite provider generates no DDL, so nothing creates the Identity schema on
-- first use. Generated once from ApplicationDbContext's model with SQLite.CodeFirst.
CREATE TABLE IF NOT EXISTS "AspNetRoles" ([Id] nvarchar (128) NOT NULL PRIMARY KEY, [Name] nvarchar (256) NOT NULL);
CREATE TABLE IF NOT EXISTS "AspNetUsers" ([Id] nvarchar (128) NOT NULL PRIMARY KEY, [Email] nvarchar (256), [EmailConfirmed] bit NOT NULL, [PasswordHash] nvarchar, [SecurityStamp] nvarchar, [PhoneNumber] nvarchar, [PhoneNumberConfirmed] bit NOT NULL, [TwoFactorEnabled] bit NOT NULL, [LockoutEndDateUtc] datetime, [LockoutEnabled] bit NOT NULL, [AccessFailedCount] int NOT NULL, [UserName] nvarchar (256) NOT NULL);
CREATE TABLE IF NOT EXISTS "AspNetUserRoles" ([UserId] nvarchar (128) NOT NULL, [RoleId] nvarchar (128) NOT NULL, PRIMARY KEY([UserId], [RoleId]), FOREIGN KEY ([RoleId]) REFERENCES "AspNetRoles"([Id]) ON DELETE CASCADE, FOREIGN KEY ([UserId]) REFERENCES "AspNetUsers"([Id]) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS "AspNetUserClaims" ([Id] INTEGER PRIMARY KEY, [UserId] nvarchar (128) NOT NULL, [ClaimType] nvarchar, [ClaimValue] nvarchar, FOREIGN KEY ([UserId]) REFERENCES "AspNetUsers"([Id]) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS "AspNetUserLogins" ([LoginProvider] nvarchar (128) NOT NULL, [ProviderKey] nvarchar (128) NOT NULL, [UserId] nvarchar (128) NOT NULL, PRIMARY KEY([LoginProvider], [ProviderKey], [UserId]), FOREIGN KEY ([UserId]) REFERENCES "AspNetUsers"([Id]) ON DELETE CASCADE);
CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex" ON "AspNetRoles" ("Name");
CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex" ON "AspNetUsers" ("UserName");
CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_UserId" ON "AspNetUserRoles" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
CREATE INDEX IF NOT EXISTS "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
