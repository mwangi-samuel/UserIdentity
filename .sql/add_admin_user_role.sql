USE useridentity;

-- Pass these with mysql --init-command="SET @username = '...', @app_name = '...'" or edit the defaults.
-- @username is matched against the user name or the email.
-- @role_name overrides the role, for example 'global:admin' for a global admin.
SET @username = COALESCE(@username, 'UNIQUE_USERNAME');
SET @app_name = COALESCE(@app_name, 'testapp');

-- Admin checks look for "<app>:admin", where <app> is the token's lowercased azp claim
SET @role_name = LOWER(COALESCE(@role_name, CONCAT(@app_name, ':admin')));

SET @user_id = (
  SELECT id
  FROM users
  WHERE normalized_user_name = UPPER(@username) OR normalized_email = UPPER(@username)
  LIMIT 1);

INSERT IGNORE INTO roles (id, name, normalized_name, concurrency_stamp)
VALUES (UUID(), @role_name, UPPER(@role_name), UUID());

SET @role_id = (
  SELECT id
  FROM roles
  WHERE normalized_name = UPPER(@role_name));

-- A plain INSERT IGNORE would store '' for a NULL user id
INSERT IGNORE INTO user_roles (user_id, role_id)
SELECT @user_id, @role_id
FROM DUAL
WHERE @user_id IS NOT NULL;

SELECT u.user_name, r.name AS role_name
FROM user_roles ur
JOIN users u ON u.id = ur.user_id
JOIN roles r ON r.id = ur.role_id
WHERE ur.user_id = @user_id;

SELECT IF(@user_id IS NULL, CONCAT('No user found for ', @username), 'Log in again to get a token with the admin role') AS result;
