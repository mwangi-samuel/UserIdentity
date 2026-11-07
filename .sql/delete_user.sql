USE useridentity;

SET @username = 'USER372';

SELECT @user_id := id
FROM users
WHERE User_Name = @username;

DELETE
FROM  refresh_tokens
WHERE User_Id = @user_id;

DELETE
FROM user_claims
WHERE User_Id = @user_id;

DELETE
FROM user_logins
WHERE User_Id = @user_id;

DELETE
FROM user_logins
WHERE User_Id = @user_id;

DELETE
FROM user_roles
WHERE User_Id = @user_id;

DELETE
FROM user_tokens
WHERE User_Id = @user_id;

DELETE
FROM user_details
WHERE Id = @user_id;

DELETE
FROM users
WHERE Id = @user_id;

DELETE
FROM user_registered_apps
WHERE user_id = @user_id;
