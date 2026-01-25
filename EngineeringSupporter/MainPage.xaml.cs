﻿﻿using EngineeringSupporter.Controls;

namespace EngineeringSupporter;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        this.TodoView.Add(new TodoView());
    }
    
}