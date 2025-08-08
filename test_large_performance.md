# Performance Test File

This is a large markdown file to test the theme switching performance optimization.

Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.

## Section 1

Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.

### Subsection 1.1

Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo.

## Section 2

Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt.

### Subsection 2.1

Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem.

### Subsection 2.2

Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur.

## Section 3 - Code Examples

```csharp
public class Example
{
    public void TestMethod()
    {
        Console.WriteLine("This is a test method");
        for (int i = 0; i < 1000; i++)
        {
            System.Threading.Thread.Sleep(1);
        }
    }
}
```

```javascript
function testFunction() {
    console.log("JavaScript test function");
    const items = [];
    for (let i = 0; i < 1000; i++) {
        items.push(i * 2);
    }
    return items;
}
```

## Section 4 - Lists

1. First item with lots of text to make this document larger and test performance with more content
2. Second item with even more text to continue testing the performance optimization
3. Third item to ensure we have enough content for meaningful performance testing
4. Fourth item with additional text content for thorough testing
5. Fifth item continuing the pattern of adding more text content

- Bullet point one with substantial text content for performance testing
- Bullet point two with more text to increase document size
- Bullet point three continuing the pattern of longer text
- Bullet point four with additional content for testing
- Bullet point five to round out the bullet list

## Section 5 - Tables

| Column 1 | Column 2 | Column 3 | Column 4 |
|----------|----------|----------|----------|
| Data 1   | Data 2   | Data 3   | Data 4   |
| Row 2    | More     | Content  | Here     |
| Row 3    | Even     | More     | Data     |
| Row 4    | Testing  | Performance | Now   |
| Row 5    | Final    | Table    | Row      |

## Section 6 - More Content

At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum deleniti atque corrupti quos dolores et quas molestias excepturi sint occaecati cupiditate non provident.

Similique sunt in culpa qui officia deserunt mollitia animi, id est laborum et dolorum fuga. Et harum quidem rerum facilis est et expedita distinctio.

Nam libero tempore, cum soluta nobis est eligendi optio cumque nihil impedit quo minus id quod maxime placeat facere possimus, omnis voluptas assumenda est, omnis dolor repellendus.

Temporibus autem quibusdam et aut officiis debitis aut rerum necessitatibus saepe eveniet ut et voluptates repudiandae sint et molestiae non recusandae.

## Section 7 - Final Performance Test Content

This section contains the final content to ensure we have a sufficiently large document to test the theme switching performance improvements. The old character-by-character processing would take 10+ seconds on content of this size, while the new bulk operations should complete in under 500ms.

The optimization includes:
- Async processing to prevent UI blocking
- Bulk text selection operations instead of character-by-character processing
- SuspendLayout() and ResumeLayout() for batched updates
- Task.Run with ConfigureAwait(false) for better performance
- Progress indicators for user feedback during theme switches
- Efficient hyperlink processing that only updates text segments between hyperlinks

This should dramatically improve the user experience when switching themes with large content.