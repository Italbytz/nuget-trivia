using System;
using System.Collections.Generic;

namespace Italbytz.Trivia.Abstractions
{
    public interface IMultipleChoiceQuestion : IQuestion
    {
        List<string> PossibleAnswers { get; set; }
        int CorrectAnswerIndex { get; set; }
    }
}
