namespace PlantProcess.Application.Common.Results;

public enum ApplicationErrorType
{
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    BusinessRule = 4,
    Unauthorized = 5,
    Forbidden = 6,
    Infrastructure = 7,
    Unexpected = 8,
    NotImplemented = 9
}


