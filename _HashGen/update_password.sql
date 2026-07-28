UPDATE Users 
SET PasswordHash = '$2a$12$4k27EpO8m9FM4qV8ITjczeID8MnkjnmIYzQ5y56Nd4hRIZLoYnnny',
	FailedLoginAttempts = 0,
	IsLocked = 0
WHERE Username = 'admin';

SELECT 'Done. Hash = ' + PasswordHash FROM Users WHERE Username = 'admin';
