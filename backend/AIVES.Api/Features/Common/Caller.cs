namespace AIVES.Api.Features.Common;

// Created by the transport layer from verified JWT claims, never from request bodies.
public readonly record struct Caller(int UserId, string Role)
{
    public bool IsAdmin => Role == "Admin";
    public bool IsLecturer => Role == "Lecturer";
}
