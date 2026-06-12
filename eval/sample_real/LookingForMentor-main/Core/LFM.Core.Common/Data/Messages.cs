namespace LFM.Core.Common.Data
{
    public static class Messages
    {
        #region CommonOperationMessage
        
        public const string SystemError = "Ой, сталась халепа...";
        public const string Unauthorized = "You are unauthorized for this action.";
        public const string DataNotFound = "{0} не знайдено.";
        public const string InvalidRequest = "Invalid request.";
        public const string RequiredField = "'{0}' є обов'язковим полем.";
        public const string AccessDenied = "Access denied.";
        public const string ActionNotAllowed = "Action not allowed.";

        #endregion

        #region Auth

        public const string LoginSuccessful = "Вхід успішний.";
        public const string LoginFailed = "Невдала спроба входу.";

        public const string LogoutSuccessful = "Ви успішно вийшли із системи.";
        public const string LogoutFailed = "Помилка при виході із системи.";

        public const string RegistrationSuccessful = "Реєстрація успішна. {0}";
        public const string RegistrationFailed = "Невдала спроба реєстрації.";
        
        #endregion

        #region User

        public const string UserWithEmailAlreadyExists = "User with email '{0}' already exists.";
        public const string UserWithPhoneAlreadyExists = "User with phone number '{0}' already exists.";
        public const string UserDoesNotHaveRole = "User doesn't have any role.";
        public const string UserClaimNotFound = "User claim of type '{0}' was not found.";
        public const string InvalidUserClaim = "Invalid User Claim.";

        #endregion

        #region Order

        public const string PersonalOrderSuccessful = "Ваша заявка відправлена, очікуйте відповіді від {0}.";
        public const string PersonalOrderFailed = "You contact request failed, please contact to administration.";
        
        public const string OrderRequestSuccessful = "Ваша заявка створена успішно.";
        public const string OrderRequestFailed = "You looking for Mentor request was failed, please contact to administration.";
        public const string OrderRequestAlreadyExist = "Ви уже створили заявку по предмету {0}.";

        #endregion
        
        public const string MentorNotAbleToGetPotentialOrders = "Ви не можете переглядати потенційні заявки.";
        
        public const string ToDoCreated = "Очікуйте на підтвердження Менеджера.";
        
        public const string EmailIncorrect = "Електронна адреса '{0}' некоректна.";
        public const string PhoneIncorrect = "Номер телефону '{0}' некоректний.";
        public const string MaxLenght = "Максимальна довжина '{0}' символів.";
        public const string MinLenght = "Мінімальна довжина '{0}' символів.";

    }
}