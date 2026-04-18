using System;

namespace Italbytz.Trivia.Abstractions
{
    public interface IYesNoQuestion : IQuestion
    {
        bool Answer { get; set; }
    }
}
