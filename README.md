<div align="center">

# **Bookstore.Mobile - Bookstore Management Application**

A Modern and User-Friendly Interface for the Bookstore Management System

[![.NET MAUI Version](https://img.shields.io/badge/.NET%20MAUI-.NET%208.0-purple)](https://dotnet.microsoft.com/en-us/apps/maui)
[![API Backend](https://img.shields.io/badge/API%20Backend-BookStoreManagement--API-blue)](https://github.com/hoangnguyenn26/BookStoreManagement-API)

</div>

## **Table of Contents**

- [Overview](#overview)
- [Key Features (Client-side)](#key-features-client-side)
- [Client Architecture (MAUI)](#client-architecture-maui)
- [Technologies Used (Client)](#technologies-used-client)
- [Screenshots](#screenshots)
- [Contributing](#contributing)
- [License](#license)

## **Overview**

**Bookstore.Mobile** is a cross-platform application built with .NET MAUI, providing an intuitive and modern user interface to interact with the **[BookStoreManagement-API](https://github.com/hoangnguyenn26/BookStoreManagement-API)**. The application allows both Customers and Staff/Admins to access and manage bookstore operations efficiently.

The app is designed to run on Android, iOS, Windows, and macOS (depending on configuration and deployment targets).

## **Key Features (Client-side)**

This MAUI application implements functionalities corresponding to the APIs provided by the backend:

*   **Customer:**
    *   🔐 **Registration & Login:** Create a new account and log in securely.
    *   🏠 **Home:** Display new books and featured promotions.
    *   📖 **Browse Products:** View categories, browse books by category, and search for books.
    *   ℹ️ **Book Details:** View full information, images, descriptions, prices, and user reviews.
    *   ❤️ **Wishlist:** Add or remove books from a personal wishlist.
    *   🛒 **Shopping Cart:** Add books to the cart, update quantities, remove items, and view the total price.
    *   👤 **Profile Management:** View personal info, manage shipping addresses (add, edit, delete, set as default).
    *   🛍️ **Checkout:** Select a shipping address, review the order, and place the order (with mock payment integration).
    *   📋 **Order History:** View the list of past orders and track order details.
    *   ⭐ **Submit Reviews:** Write and submit ratings and reviews for purchased books.
    *   🔑 **Logout.**

*   **Admin & Staff (Features are displayed dynamically based on user role upon login):**
    *   📊 **Management Dashboard:** View quick overview metrics (revenue, new orders, etc.).
    *   📦 **Order Management:** View all orders, filter by status, view details, and update order statuses.
    *   📚 **Product Management:** View lists, Add/Edit/Delete Books, Categories, and Authors. Upload cover images.
    *   🚚 **Inventory Management:** Create goods receipt notes, view receipt history, and perform manual inventory adjustments.
    *   🏷️ **Promotion Management:** View lists, Add/Edit/Delete promo codes.
    *   👥 **User Management (Admin):** View user lists, view user details, activate/deactivate accounts, and (optionally) change roles.
    *   📈 **Reports:** Access reports for Revenue, Best-Selling Books, and Low Inventory (charts included).

## **Client Architecture (MAUI)**

The MAUI app is built following the **MVVM (Model-View-ViewModel)** architecture to ensure a clean separation of concerns and maintainability:

-   **Views:** XAML files that define the user interface alongside minimal code-behind (`.xaml.cs`) logic.
-   **ViewModels:** Classes containing presentation logic, View state, and Commands bound to the View. Powered by `CommunityToolkit.Mvvm`.
-   **Models/DTOs:** Classes representing data (often DTOs mirrored or referenced from the API project) used across ViewModels and Views.
-   **Services:** Classes responsible for specific tasks such as executing API calls, managing authentication states, and navigation.
-   **Interfaces:** Define contracts for Services and API Clients for better testability and dependency injection.
-   **Handlers:** Custom `DelegatingHandler` classes to manage cross-cutting concerns, such as automatically attaching authentication tokens to requests.
-   **Converters:** `IValueConverter` implementations for transforming data during UI binding.
-   **Shell:** Utilizes .NET MAUI Shell for the primary navigation structure (Flyout/Tabs) and routing.

## **Technologies Used (Client)**

-   **Framework:** .NET MAUI (on .NET 8.0 / .NET 9 Preview)
-   **Language:** C#
-   **UI:** XAML
-   **Architecture:** MVVM (using `CommunityToolkit.Mvvm`)
-   **API Calling:** Refit (with `System.Text.Json` or `Newtonsoft.Json`)
-   **Navigation:** .NET MAUI Shell
-   **Dependency Injection:** Built-in .NET MAUI DI (`Microsoft.Extensions.DependencyInjection`)
-   **Secure Storage:** `Microsoft.Maui.Storage.SecureStorage` (for JWT tokens)
-   **Preferences Storage:** `Microsoft.Maui.Storage.Preferences`
-   **Image Processing:** Standard MAUI Image controls (can consider `FFImageLoading.Maui` or `CommunityToolkit.Maui.ImageCaching` for optimization).
-   **Charts:** `Microcharts.Maui`
-   **Logging:** `Microsoft.Extensions.Logging`

## **Screenshots**

![image](https://github.com/user-attachments/assets/9e169556-c279-4f95-abcc-27a3da8da699)
![image](https://github.com/user-attachments/assets/557ab8e8-3f4a-42cb-bcab-9421ee0db327)
![image](https://github.com/user-attachments/assets/e0cb10df-2bd6-4618-9c70-b9113db92f40)
![image](https://github.com/user-attachments/assets/1542ff06-672c-4b85-bdb9-23f03b8470b8)
![image](https://github.com/user-attachments/assets/dc1f991d-c649-4867-b153-f550dad942a4)
![image](https://github.com/user-attachments/assets/fb5cdc1c-d819-4736-a0fe-6e8c7c0c8b2e)

## **Contributing**

Contributions, issues, and feature requests are welcome! Feel free to check the [issues page](../../issues).

## **License**

This project is licensed under the [MIT License](LICENSE) - see the LICENSE file for details.
