# Agents Documentation

This document provides a comprehensive overview of all available agents in the system, their capabilities, and when to use each one.

## Agent Overview

The system uses specialized agents that work together to handle different aspects of software development. Each agent has specific responsibilities, tools, and expertise.

## Available Agents

### 1. **Architect** (architect)
**Color:** Purple  
**Tools:** Read, Glob, Grep, Task, TodoWrite  
**Purpose:** Main orchestrator agent that analyzes requirements, creates implementation plans, and delegates tasks to specialized agents.

**When to use:**
- Breaking down complex tasks into manageable components
- Coordinating work between multiple specialized agents
- Creating comprehensive implementation plans
- Starting new features or major changes
- When you need high-level planning and task delegation

**Key responsibilities:**
- Requirement analysis and understanding
- Task breakdown and delegation
- Workflow coordination between agents
- Ensuring proper task sequencing

### 2. **Code Writer** (code-writer)
**Tools:** Read, Write, Edit, MultiEdit, Glob, Grep, LS, Bash  
**Purpose:** Implements features and writes code across multiple technology stacks including React, Python, C#, and RhinoCommon.

**When to use:**
- Implementing new features
- Writing new code files
- Refactoring existing code
- Making any code modifications
- Adding functionality to the codebase

**Key responsibilities:**
- Writing clean, functional code
- Following project conventions
- Implementing features according to specifications
- Making code modifications and improvements

### 3. **Code Reviewer** (code-reviewer)
**Tools:** Read, Glob, Grep, Bash  
**Model:** Sonnet  
**Color:** Yellow  
**Purpose:** Reviews all code for quality, checking syntax, style, security, performance, and best practices.

**When to use:**
- After significant code changes
- Before committing important features
- When code quality needs assessment
- To catch potential bugs or security issues
- For performance optimization suggestions

**Key responsibilities:**
- Code quality assessment
- Security vulnerability identification
- Performance bottleneck detection
- Best practices enforcement
- Maintainability evaluation

### 4. **Debugger** (debugger)
**Tools:** Read, Write, Edit, MultiEdit, Glob, Grep, LS, Bash  
**Model:** Sonnet  
**Color:** Pink  
**Purpose:** Identifies and fixes bugs through systematic debugging, root cause analysis, and comprehensive testing.

**When to use:**
- When bugs are reported
- Investigating unexpected behavior
- Tracing execution flow issues
- Fixing errors and exceptions
- Performance troubleshooting

**Key responsibilities:**
- Bug investigation and reproduction
- Root cause analysis
- Fix implementation
- Verification of solutions
- Prevention recommendations

### 5. **Documentation Writer** (documentation-writer)
**Tools:** Read, Write, Edit, MultiEdit, Glob, Grep, LS, Bash  
**Model:** Sonnet  
**Color:** Green  
**Purpose:** Creates and maintains technical documentation, API references, README files, and inline code comments.

**When to use:**
- Creating or updating README files
- Writing API documentation
- Adding code comments and docstrings
- Creating user guides or tutorials
- Documenting architecture decisions

**Key responsibilities:**
- Technical documentation creation
- Code documentation (comments, docstrings)
- User documentation and guides
- Maintaining documentation accuracy
- Creating examples and tutorials

### 6. **Git Manager** (git-manager)
**Tools:** Read, Glob, Grep, Bash  
**Model:** Sonnet  
**Color:** Red  
**Purpose:** Handles all git operations including micro-commits for checkpoints and feature-based commits.

**When to use:**
- Creating commits (micro or feature)
- Managing branches
- Handling git operations
- Creating pull requests
- Maintaining repository cleanliness

**Key responsibilities:**
- Micro-commits for development checkpoints
- Feature commits with comprehensive messages
- Branch management
- Repository maintenance
- Following conventional commit standards

### 7. **Project Context Manager** (project-context-manager)
**Tools:** Read, Write, Edit, Glob, Grep  
**Model:** Sonnet  
**Color:** Blue  
**Purpose:** Maintains PLANNING.md with comprehensive project information, features, tech stack, and user personas.

**When to use:**
- Updating project information
- Recording architecture decisions
- Documenting feature completions
- Maintaining project context
- Updating technology stack information

**Key responsibilities:**
- PLANNING.md maintenance
- Feature tracking (planned, in-progress, completed)
- Technology stack documentation
- User persona management
- Architecture decision records

### 8. **Software Engineer** (software-engineer)
**Model:** Sonnet  
**Color:** Blue  
**Purpose:** Expert software engineering assistance for complex technical problems, system design, code architecture decisions, performance optimization, or deep technical analysis.

**When to use:**
- Designing distributed systems or complex architectures
- Solving challenging algorithm problems
- Performance optimization and analysis
- Memory leak investigations
- Complex technical decision making
- System scalability planning

**Key responsibilities:**
- System architecture design
- Algorithm optimization
- Performance engineering
- Complex problem solving
- Technical best practices guidance

### 9. **Task Manager** (task-manager)
**Tools:** Read, Write, Edit, Glob, Grep  
**Model:** Sonnet  
**Color:** Orange  
**Purpose:** Manages TASKS.md for active task tracking and automatically archives completed tasks to ARCHIVED_TASKS.md.

**When to use:**
- Creating new tasks
- Updating task status
- Tracking work progress
- Archiving completed tasks
- Organizing development work

**Key responsibilities:**
- Task creation and organization
- Status management (pending, in-progress, completed)
- Automatic archiving at 20 completed tasks
- Maintaining task visibility
- Grouping related tasks

### 10. **Test Writer** (test-writer)
**Tools:** Read, Write, Edit, MultiEdit, Glob, Grep, LS, Bash  
**Model:** Sonnet  
**Color:** Cyan  
**Purpose:** Creates and maintains comprehensive test suites including unit tests, integration tests, and test coverage.

**When to use:**
- Writing unit tests for new code
- Creating integration tests
- Developing end-to-end tests
- Updating tests for code changes
- Improving test coverage

**Key responsibilities:**
- Test creation (unit, integration, e2e)
- Test maintenance and updates
- Edge case coverage
- Test strategy planning
- Framework-specific testing

### 11. **Web Search** (web-search)
**Tools:** WebSearch, WebFetch  
**Color:** Blue  
**Purpose:** Performs web searches and fetches content from the internet for documentation, tutorials, and resources.

**When to use:**
- Finding online documentation for libraries or frameworks
- Searching for tutorials or implementation guides
- Getting up-to-date information beyond Claude's knowledge cutoff
- Researching best practices and industry standards
- Finding code examples and solutions online
- Looking up API documentation
- Researching error messages and solutions
- Finding Windows Forms or .NET specific resources

**Key responsibilities:**
- Executing targeted web searches
- Fetching specific web content
- Extracting relevant information
- Providing summaries of findings
- Citing sources
- Filtering results for relevance
- Accessing current documentation

**Important:** Always use this agent when you need current information, documentation that might have been updated, or when searching for solutions to specific problems that require online resources.

## Agent Collaboration Workflow

### Typical Feature Implementation Flow:
1. **Architect** receives requirements and creates implementation plan
2. **Task Manager** creates specific tasks in TASKS.md
3. **Project Context Manager** updates PLANNING.md if needed
4. **Code Writer** implements the feature
5. **Git Manager** creates micro-commits during development
6. **Code Reviewer** reviews the implementation
7. **Test Writer** creates tests for new code
8. **Debugger** fixes any issues found
9. **Documentation Writer** updates relevant documentation
10. **Git Manager** creates final feature commit
11. **Task Manager** marks tasks as completed and archives when needed

### For Complex Technical Problems:
1. **Software Engineer** analyzes the problem and provides solution design
2. **Architect** breaks down the solution into tasks
3. Standard implementation flow follows

### For Bug Fixes:
1. **Debugger** investigates and identifies root cause
2. **Code Writer** implements the fix
3. **Test Writer** adds tests to prevent regression
4. **Code Reviewer** verifies the fix
5. **Git Manager** commits the changes

## Best Practices

1. **Always start with the Architect** for new features or complex tasks
2. **Use Software Engineer** for complex technical decisions before implementation
3. **Ensure Code Reviewer** checks code before major commits
4. **Keep PLANNING.md and TASKS.md updated** through respective managers
5. **Create micro-commits frequently** for easy rollback capabilities
6. **Document as you go** rather than as an afterthought
7. **Write tests alongside code** not after implementation

## Important Notes

- Each agent is specialized and should be used for its intended purpose
- Agents work together in coordinated workflows
- The Architect orchestrates multi-agent workflows
- All agents reference PLANNING.md for project context
- TASKS.md is the single source of truth for active work
- Git Manager provides checkpoint functionality through micro-commits
- The system is optimized for solo developer workflow