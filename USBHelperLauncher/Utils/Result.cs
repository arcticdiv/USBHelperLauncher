using System;

namespace USBHelperLauncher.Utils
{
    class ResultException : Exception
    {
        public ResultException(string message) : base(message) { }
    }


    class Result
    {
        public bool IsSuccess { get; }
        public string ErrorData { get; }

        protected Result(bool isSuccess, string errorData)
        {
            IsSuccess = isSuccess;
            ErrorData = errorData;
        }

        public static Result Success()
        {
            return new Result(true, null);
        }

        public static Result Failure(string errorData)
        {
            return new Result(false, errorData);
        }

        public void EnsureSuccess()
        {
            if (!IsSuccess)
            {
                throw new ResultException(ErrorData);
            }
        }
    }


    class Result<TValue> : Result
    {
        public TValue Value { get; }

        protected Result(bool isSuccess, TValue value, string errorData) : base(isSuccess, errorData)
        {
            Value = value;
        }

        public static Result<TValue> Success(TValue value)
        {
            return new Result<TValue>(true, value, null);
        }

        [Obsolete("Use Success(value) instead", true)]
        new public static Result Success()
        {
            throw new InvalidOperationException();
        }

        new public static Result<TValue> Failure(string errorData)
        {
            return new Result<TValue>(false, default, errorData);
        }
    }
}
