using System.Text.RegularExpressions;

namespace MonopolyGame.Multiplayer
{
    public static class InputValidator
    {
        private static readonly Regex UsernamePattern = new Regex(@"^[a-z0-9]+$", RegexOptions.Compiled);
        private static readonly Regex PasswordPattern = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[^A-Za-z]).{8,}$", RegexOptions.Compiled);

        public static MultiplayerError ValidateSignUp(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new MultiplayerError("signup_username_blank", "Username cannot be blank.");
            }

            if (!UsernamePattern.IsMatch(username))
            {
                return new MultiplayerError(
                    "signup_username_format",
                    "Username can only contain lowercase a-z and numbers. No spaces or special characters are allowed.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return new MultiplayerError("signup_password_blank", "Password cannot be blank.");
            }

            if (!PasswordPattern.IsMatch(password))
            {
                return new MultiplayerError(
                    "signup_password_format",
                    "Password must be at least 8 characters and include uppercase, lowercase, and a special symbol.");
            }

            return null;
        }

        public static MultiplayerError ValidateSignIn(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new MultiplayerError("signin_username_blank", "Username cannot be blank.");
            }

            if (!UsernamePattern.IsMatch(username))
            {
                return new MultiplayerError(
                    "signin_username_format",
                    "Username can only contain lowercase a-z and numbers. No spaces or special characters are allowed.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return new MultiplayerError("signin_password_blank", "Password cannot be blank.");
            }

            return null;
        }
    }
}
